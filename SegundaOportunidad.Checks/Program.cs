using System.Net;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SegundaOportunidad.Controllers;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;
using SegundaOportunidad.Services;

if (args.Length == 1 && args[0] == "--demo-ui-checks") { await DemoUiChecks.Run(); return; }
if (args.Length == 1 && args[0] == "--demo-safety-checks") { await DemoSafetyChecks.Run(); return; }
if (args.Length == 2 && args[0] == "--demo-setup") { await DemoSetupChecks.Run(args[1]); return; }
if (args.Length == 1 && args[0] == "--ml-checks") { await MlChecks.Run(); return; }
if (args.Length == 1 && args[0] == "--production-smoke") { await ProductionSmokeChecks.Run(); return; }
if (args.Length == 1 && args[0] == "--demo-smoke") { await ProductionSmokeChecks.Run("Demo"); return; }
if (args.Length == 1 && args[0] == "--integration-read") { await IntegrationReadChecks.Run(); return; }
if (args.Length == 1 && args[0] == "--deployment-checks") { await DeploymentChecks.Run(); return; }
if (args.Length == 2 && args[0] == "--chat-catalog-live") { await GeminiLiveChecks.Run(args[1]); return; }
if (args.Length == 1 && args[0] == "--chat-catalog-checks") { await ChatCatalogChecks.Run(); return; }
if (args.Length == 1 && args[0] == "--gemini-live") { await GeminiLiveChecks.Run(); return; }
if (args.Length == 1 && args[0] == "--gemini-checks") { await GeminiChatChecks.Run(); return; }
if (args.Length == 2 && args[0] == "--seed-demo-publicaciones") { await DemoPublicaciones.Run(args[1]); return; }
if (args.Length == 2 && args[0] == "--upgrade-db") { await DatabaseUpgrade.Run(args[1]); return; }
if (args.Length == 2 && args[0] == "--verify-db") { await DatabaseVerification.Run(args[1]); return; }
if (args.Length == 2 && args[0] == "--bootstrap-admin") { await AdminBootstrap.Run(args[1]); return; }
if (args.Length == 2 && args[0] == "--reset-content")
{
    try { await ResetContent.Run(args[1]); }
    catch(Exception ex) { Console.Error.WriteLine("No se completó la limpieza: "+ex.Message); Environment.ExitCode=1; }
    return;
}
if (args.Length == 1 && args[0] == "--smtp-live")
{
    try { await SmtpLiveChecks.Run(); }
    catch (System.Net.Mail.SmtpException ex) { Console.Error.WriteLine("La prueba SMTP falló: " + ex.StatusCode); Environment.ExitCode=1; }
    catch (Exception ex) { Console.Error.WriteLine("La prueba SMTP no se completó: " + ex.GetType().Name); Environment.ExitCode=1; }
    return;
}
if (args.Length == 1 && args[0] == "--guard-live")
{
    try { await GuardLiveChecks.Run(); }
    catch (Exception ex) { Console.Error.WriteLine("La prueba real no se completó: " + ex.Message); Environment.ExitCode = 1; }
    return;
}

try
{
static void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FALLÓ: " + name);
    Console.WriteLine("OK: " + name);
}
await using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
await using (var migrated = new SqliteConnection("Data Source=:memory:"))
{
    await migrated.OpenAsync();
    await using var migrationDb = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(migrated).Options);
    await migrationDb.Database.MigrateAsync();
    Check(!(await migrationDb.Database.GetPendingMigrationsAsync()).Any(), "Migraciones completas sobre SQLite vacío");
}
var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
await using (var db = new ApplicationDbContext(options))
{
    await db.Database.EnsureCreatedAsync();
    db.Users.Add(new ApplicationUser { Id = "u", UserName = "prueba" });
    db.Users.Add(new ApplicationUser { Id = "checks", UserName = "admin-checks" });
    db.Categorias.AddRange(new Categoria { Id = 1, Name = "Libros" }, new Categoria { Id = 2, Name = "Ropa" });
    db.Articulos.AddRange(
        new Articulo { Id = "a", UserId = "u", CategoriaId = 1, CreatedAt = new(2026, 1, 31, 23, 59, 59) },
        new Articulo { Id = "b", UserId = "u", CategoriaId = 2, IsActive = false, CreatedAt = new(2026, 1, 10) },
        new Articulo { Id = "c", UserId = "u", CategoriaId = 1, CreatedAt = new(2026, 2, 1) });
    db.Intercambios.Add(new Intercambio { ProposerId = "u", ReceiverId = "u", OfferedArticleId = "b", RequestedArticleId = "a", Estado = EstadoIntercambio.Completado, CreatedAt = new(2026, 1, 20) });
    await db.SaveChangesAsync();
    var service = new AdminDashboardService(db);
    var enero = await service.ObtenerAsync(new(2026, 1, 1), new(2026, 1, 31), null);
    Check(enero.Usuarios == 2 && enero.PublicacionesActivas == 1 && enero.PublicacionesInactivas == 1, "Totales y fin de fecha inclusive");
    Check(enero.Completados == 1 && enero.Actividad.Single().Total == 2, "Estados y agregaciones mensuales SQL");
    var libros = await service.ObtenerAsync(new(2026, 1, 1), new(2026, 1, 31), 1);
    Check(libros.PublicacionesInactivas == 0 && libros.Completados == 1 && libros.PorCategoria.Single().Etiqueta == "Libros", "Filtro de categoría y artículo solicitado");
    var vacio = await service.ObtenerAsync(new(2026, 3, 1), new(2026, 4, 30), null);
    Check(vacio.Actividad.Count == 2 && vacio.Actividad.All(x => x.Total == 0) && vacio.PorCategoria.Count == 0, "Período vacío y meses con cero");
    var controller = new AdminController(service, db);
    Check(await controller.Index(new(2026, 2, 1), new(2026, 1, 1), null, default) is Microsoft.AspNetCore.Mvc.BadRequestObjectResult, "Fechas invertidas rechazadas");
    Check(await controller.Index(null, null, 999, default) is Microsoft.AspNetCore.Mvc.BadRequestObjectResult, "Categoría inexistente rechazada");
    var moderation = new ModeracionPublicacionService(db);
    var article = await db.Articulos.SingleAsync(a => a.Id == "a");
    var version = article.ModeracionVersion;
    await moderation.CambiarAsync("a", "u", EstadoModeracion.Oculta, "Revisión de contenido", version, default);
    Check(!await db.Articulos.AnyAsync(a => a.Id == "a") && await db.Articulos.IgnoreQueryFilters().AnyAsync(a => a.Id == "a"), "Ocultar excluye del catálogo sin borrar");
    Check(await db.ModeracionesPublicaciones.CountAsync() == 1, "Decisión y responsable persistidos");
    Check((await service.ObtenerAsync(new(2026, 1, 1), new(2026, 1, 31), null)).Ocultas == 1, "Dashboard contabiliza publicaciones moderadas");
    try { await moderation.CambiarAsync("a", "u", EstadoModeracion.Visible, "Restaurar artículo", version, default); throw new Exception("No detectó versión obsoleta"); }
    catch (DbUpdateConcurrencyException) { Console.WriteLine("OK: Conflicto de moderación detectado"); }
    try { await moderation.CambiarAsync("a", "u", EstadoModeracion.Visible, "", article.ModeracionVersion, default); throw new Exception("Aceptó motivo vacío"); }
    catch (ArgumentException) { Console.WriteLine("OK: Motivo obligatorio"); }
    await moderation.CambiarAsync("a", "u", EstadoModeracion.Eliminada, "Eliminación justificada", article.ModeracionVersion, default);
    Check(await db.Intercambios.CountAsync() == 1, "Eliminación lógica conserva intercambios");
    await moderation.CambiarAsync("a", "u", EstadoModeracion.Visible, "Restauración justificada", article.ModeracionVersion, default);
    Check(await db.Articulos.AnyAsync(a => a.Id == "a") && await db.ModeracionesPublicaciones.CountAsync() == 3, "Restauración e historial completo");
}
var builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(connection));
builder.Services.AddScoped<AdminDashboardService>();
builder.Services.AddScoped<ModeracionPublicacionService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<SancionUsuarioService>();
builder.Services.AddScoped<ReporteService>();
builder.Services.AddSingleton(new AlgoliaService(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()));
builder.Services.AddIdentityCore<ApplicationUser>().AddRoles<Microsoft.AspNetCore.Identity.IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>().AddSignInManager<SuspensionSignInManager>();
builder.Services.AddAuthentication("Checks").AddScheme<AuthenticationSchemeOptions, CheckAuthHandler>("Checks", _ => { });
builder.Services.AddAuthorization();
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(AdminController).Assembly);
await using var app = builder.Build();
app.UseAuthentication();
app.UseMiddleware<SuspensionMiddleware>();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller}/{action=Index}");
// El probe reutiliza los atributos reales del controlador; evita depender de archivos Razor en esta prueba de acceso.
app.MapGet("/probe", () => "permitido").RequireAuthorization(typeof(AdminController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().ToArray());
app.MapGet("/Articulos/Create", () => "crear");
await app.StartAsync();
using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
Check((await client.GetAsync("/probe")).StatusCode == HttpStatusCode.Unauthorized, "Visitante recibe 401");
client.DefaultRequestHeaders.Add("X-Check-Role", "Usuario");
Check((await client.GetAsync("/probe")).StatusCode == HttpStatusCode.Forbidden, "Usuario común recibe 403");
client.DefaultRequestHeaders.Remove("X-Check-Role");
client.DefaultRequestHeaders.Add("X-Check-Role", "Admin");
Check((await client.GetAsync("/probe")).StatusCode == HttpStatusCode.OK, "Admin autorizado");
Check((await client.GetAsync("/Admin?categoriaId=999")).StatusCode == HttpStatusCode.BadRequest, "Ruta MVC real aplica validación");
var pagina = await client.GetAsync("/Admin?desde=2026-01-01&hasta=2026-01-31");
Check(pagina.StatusCode == HttpStatusCode.OK && (await pagina.Content.ReadAsStringAsync()).Contains("Centro de administración"), "Dashboard Razor responde con datos reales");
Check(typeof(SupervisorController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Any(a => a.Roles == "Admin"), "Supervisor requiere Admin");
client.DefaultRequestHeaders.Remove("X-Check-Role");
Check((await client.GetAsync("/AdminPublicaciones")).StatusCode == HttpStatusCode.Unauthorized, "Publicaciones requiere login");
client.DefaultRequestHeaders.Add("X-Check-Role", "Usuario");
Check((await client.GetAsync("/AdminPublicaciones")).StatusCode == HttpStatusCode.Forbidden, "Publicaciones rechaza usuario común");
client.DefaultRequestHeaders.Remove("X-Check-Role");
client.DefaultRequestHeaders.Add("X-Check-Role", "Admin");
Check((await client.GetAsync("/AdminPublicaciones")).StatusCode == HttpStatusCode.OK, "Tabla administrativa renderizada");
Check((await client.GetAsync("/AdminPublicaciones/Detalle?id=a")).StatusCode == HttpStatusCode.OK, "Detalle con historial renderizado");
Check((await client.PostAsync("/AdminPublicaciones/Cambiar", new FormUrlEncodedContent(new Dictionary<string,string> { ["id"]="a", ["motivo"]="Intento sin token" }))).StatusCode == HttpStatusCode.BadRequest, "Moderación rechaza POST sin antiforgery");
await UserChecks.Run(client, options, app.Services);
await app.StopAsync();
await RealIdentityChecks.Run();
await ChatCatalogChecks.Run();
await DeploymentChecks.Run();
await MlChecks.Run();
await DemoSafetyChecks.Run();
await LegacyTokensChecks.Run();
Console.WriteLine("Todas las comprobaciones pasaron. No se utilizó app.db ni servicios externos.");
}
catch(Exception ex)
{
    Console.Error.WriteLine("Comprobaciones fallidas: " + ex.Message);
    Environment.ExitCode=1;
}

sealed class CheckAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Check-Role", out var role)) return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "checks"), new Claim(ClaimTypes.Role, role.ToString()) }, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
