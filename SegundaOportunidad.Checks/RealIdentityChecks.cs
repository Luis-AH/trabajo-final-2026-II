using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SegundaOportunidad.Controllers;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;
using SegundaOportunidad.Services;

internal static class RealIdentityChecks
{
    static void Check(bool value, string label) { if (!value) throw new Exception(label); Console.WriteLine("OK REAL: " + label); }
    public static async Task Run()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(AdminController).Assembly.GetName().Name, ContentRootPath = Path.GetFullPath("SegundaOportunidad") });
        builder.Logging.ClearProviders();
        using (var cache = new RedisService(builder.Configuration, Microsoft.Extensions.Logging.Abstractions.NullLogger<RedisService>.Instance))
        {
            Check(await cache.GetCacheAsync<string>("checks") == null, "Redis sin configurar devuelve fallo de caché");
            await cache.SetCacheAsync("checks", "valor");
            await cache.RemoveCacheAsync("checks");
        }
        builder.Configuration["Redis:ConnectionString"] = "127.0.0.1:1,connectTimeout=100,connectRetry=0,abortConnect=false";
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(connection));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddDefaultIdentity<ApplicationUser>().AddRoles<IdentityRole>().AddSignInManager<SuspensionSignInManager>().AddEntityFrameworkStores<ApplicationDbContext>();
        builder.Services.AddScoped<AdminDashboardService>();
        builder.Services.AddScoped<ModeracionPublicacionService>();
        builder.Services.AddScoped<SancionUsuarioService>();
        builder.Services.AddScoped<ReporteService>();
        builder.Services.AddSingleton<IAdminGuardClient,GuardChecks.TestClient>();
        builder.Services.AddSingleton<IAdminGuardImageReader,GuardChecks.TestImage>();
        builder.Services.AddSingleton<AdminGuardGate>();
        builder.Services.AddScoped<AdminGuardService>();
        builder.Services.AddSingleton<RedisService>();
        builder.Services.AddSingleton<AlgoliaService,PublicationChecks.TestAlgolia>();
        builder.Services.AddControllersWithViews(o => o.Filters.Add<VerifiedAccessFilter>());
        builder.Services.AddAccountSecurity(builder.Configuration);
        builder.Services.AddSingleton<IRecoveryEmailService,SecurityChecks.Mailbox>();
        builder.Services.AddSingleton<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender,RegistrationChecks.TestEmailSender>();
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseMiddleware<SuspensionMiddleware>();
        app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        app.MapRazorPages();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.MigrateAsync();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            await roles.CreateAsync(new IdentityRole("Admin"));
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            foreach (var id in new[] { "admin", "member" })
            {
                var u = new ApplicationUser { Id = id, UserName = id + "@checks.local", Email = id + "@checks.local", EmailConfirmed = true, IsVerified=true, VerifiedAtUtc=DateTime.UtcNow, VerifiedByAdminId="admin" };
                Check((await users.CreateAsync(u, "Checks123!" )).Succeeded, "Cuenta temporal " + id);
                if (id == "admin") await users.AddToRoleAsync(u, "Admin");
            }
            db.Categorias.Add(new Categoria { Id = 1, Name = "Libros" });
            db.Articulos.Add(new Articulo { Id = "art", UserId = "member", CategoriaId = 1, Title = "Artículo de prueba", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        await app.StartAsync();
        var address = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
        using var admin = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = address };
        using var member = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = address };
        static async Task<string> Token(HttpClient client, string url)
        {
            var response = await client.GetAsync(url);
            var html = await response.Content.ReadAsStringAsync();
            var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            Check(response.IsSuccessStatusCode && match.Success, "Formulario y antiforgery: " + url);
            return WebUtility.HtmlDecode(match.Groups[1].Value);
        }
        async Task Login(HttpClient client, string name)
        {
            var token = await Token(client, "/Identity/Account/Login");
            var response = await client.PostAsync("/Identity/Account/Login?returnUrl=%2FAdmin", new FormUrlEncodedContent(new Dictionary<string,string> { ["Input.Email"] = name + "@checks.local", ["Input.Password"] = "Checks123!", ["__RequestVerificationToken"] = token }));
            Check(response.StatusCode == HttpStatusCode.Redirect, "Login Identity y cookie: " + name);
        }
        Check((await admin.GetAsync("/Admin")).StatusCode == HttpStatusCode.Redirect, "Anónimo redirigido a login");
        await Login(admin, "admin"); await Login(member, "member");
        Check((await admin.GetAsync("/Admin")).StatusCode == HttpStatusCode.OK, "Admin real accede al dashboard");
        Check((await admin.GetAsync("/Home/Dashboard")).StatusCode == HttpStatusCode.OK, "Dashboard personal consulta SQLite cuando Redis es inaccesible");
        using (var scope = app.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<RedisService>().RemoveCacheAsync("checks");
            Console.WriteLine("OK REAL: Invalidación tolera Redis inaccesible");
        }
        Check((await member.GetAsync("/AdminUsuarios")).StatusCode == HttpStatusCode.Redirect, "Cookie de usuario común denegada");
        var tokenModeracion = await Token(admin, "/AdminPublicaciones/Detalle/art");
        string version;
        using (var scope = app.Services.CreateScope()) version = (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Articulos.SingleAsync()).ModeracionVersion;
        var hidden = await admin.PostAsync("/AdminPublicaciones/Cambiar", new FormUrlEncodedContent(new Dictionary<string,string> { ["id"]="art", ["estado"]="Oculta", ["motivo"]="Revisión de prueba", ["version"]=version, ["__RequestVerificationToken"]=tokenModeracion }));
        Check(hidden.StatusCode == HttpStatusCode.Redirect, "Ocultación con cookie y token reales");
        using (var scope = app.Services.CreateScope()) version = (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Articulos.IgnoreQueryFilters().SingleAsync()).ModeracionVersion;
        var incomplete = await admin.PostAsync("/AdminPublicaciones/Cambiar", new FormUrlEncodedContent(new Dictionary<string,string> { ["id"]="art", ["motivo"]="Acción omitida", ["version"]=version, ["__RequestVerificationToken"]=tokenModeracion }));
        Check(incomplete.StatusCode == HttpStatusCode.BadRequest, "Omitir acción no restaura por defecto");
        Check((await member.GetAsync("/Articulos/Details/art")).StatusCode == HttpStatusCode.NotFound, "Detalle público bloqueado tras ocultación");
        var tokenSancion = await Token(admin, "/AdminUsuarios/Detalle/member");
        using (var scope = app.Services.CreateScope()) version = (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.SingleAsync(u => u.Id == "member")).SanctionVersion;
        var suspended = await admin.PostAsync("/AdminUsuarios/Sancionar", new FormUrlEncodedContent(new Dictionary<string,string> { ["id"]="member", ["tipo"]="SuspenderCuenta", ["horas"]="1", ["motivo"]="Suspensión de prueba", ["version"]=version, ["__RequestVerificationToken"]=tokenSancion }));
        Check(suspended.StatusCode == HttpStatusCode.Redirect, "Sanción con cookie y token reales");
        Check((await member.GetAsync("/Articulos")).StatusCode == HttpStatusCode.Forbidden, "Cookie existente bloqueada inmediatamente");
        using var suspendedClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = address };
        var loginToken = await Token(suspendedClient, "/Identity/Account/Login");
        var deniedLogin = await suspendedClient.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(new Dictionary<string,string> { ["Input.Email"]="member@checks.local", ["Input.Password"]="Checks123!", ["__RequestVerificationToken"]=loginToken }));
        Check(deniedLogin.StatusCode == HttpStatusCode.OK && (await suspendedClient.GetAsync("/Admin")).StatusCode == HttpStatusCode.Redirect, "Login HTTP real deniega cuenta suspendida");
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Check(await db.SancionesUsuarios.CountAsync() == 1 && await db.ModeracionesPublicaciones.CountAsync() == 1, "Auditorías persistidas por flujo HTTP");
        }
        using (var scope = app.Services.CreateScope()) version = (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.SingleAsync(u => u.Id == "member")).SanctionVersion;
        var reactivate = await admin.PostAsync("/AdminUsuarios/Sancionar", new FormUrlEncodedContent(new Dictionary<string,string> { ["id"]="member", ["tipo"]="ReactivarCuenta", ["motivo"]="Reactivación de prueba", ["version"]=version, ["__RequestVerificationToken"]=tokenSancion }));
        Check(reactivate.StatusCode == HttpStatusCode.Redirect, "Reactivación por formulario real");
        Check((await member.GetAsync("/Articulos/Details/art")).StatusCode == HttpStatusCode.NotFound, "Cuenta reactivada conserva moderación de artículo");
        using (var scope = app.Services.CreateScope()) version = (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Articulos.IgnoreQueryFilters().SingleAsync()).ModeracionVersion;
        var restored = await admin.PostAsync("/AdminPublicaciones/Cambiar", new FormUrlEncodedContent(new Dictionary<string,string> { ["id"]="art", ["estado"]="Visible", ["motivo"]="Restauración de prueba", ["version"]=version, ["__RequestVerificationToken"]=tokenModeracion }));
        Check(restored.StatusCode == HttpStatusCode.Redirect && (await member.GetAsync("/Articulos/Details/art")).StatusCode == HttpStatusCode.OK, "Restauración devuelve detalle público");
        await ReportChecks.Run(admin, member, app.Services);
        await GuardWorkspaceChecks.Run(admin,member,app.Services);
        await GuardChecks.Run(admin,member,app.Services);
        await VerificationChecks.Run(admin,app.Services);
        await SecurityChecks.Run(admin,member,app.Services);
        await RegistrationChecks.Run(services:app.Services,address:admin.BaseAddress!);
        await PublicationChecks.Run(admin,app.Services);
        await app.StopAsync();
    }
}
