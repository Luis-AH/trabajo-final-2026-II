using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Data;
using SegundaOportunidad.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<SegundaOportunidad.Models.ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddRoles<IdentityRole>()
    .AddSignInManager<SegundaOportunidad.Services.SuspensionSignInManager>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddSingleton<SegundaOportunidad.Services.AlgoliaService>();
builder.Services.AddScoped<SegundaOportunidad.Services.AdminDashboardService>();
builder.Services.AddScoped<SegundaOportunidad.Services.ModeracionPublicacionService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<SegundaOportunidad.Services.SancionUsuarioService>();
builder.Services.AddScoped<SegundaOportunidad.Services.ReporteService>();
builder.Services.Configure<SegundaOportunidad.Services.AdminGuardOptions>(builder.Configuration.GetSection("AdminGuard"));
builder.Services.AddSingleton<SegundaOportunidad.Services.AdminGuardGate>();
builder.Services.AddScoped<SegundaOportunidad.Services.AdminGuardService>();
builder.Services.AddHttpClient<SegundaOportunidad.Services.IAdminGuardClient, SegundaOportunidad.Services.AdminGuardClient>(client => client.Timeout = TimeSpan.FromSeconds(45))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddHttpClient<SegundaOportunidad.Services.IAdminGuardImageReader, SegundaOportunidad.Services.AdminGuardImageReader>(client => client.Timeout = TimeSpan.FromSeconds(15))
    .ConfigurePrimaryHttpMessageHandler(SegundaOportunidad.Services.AdminGuardImageReader.Handler);

builder.Services.AddHttpClient<AiSearchService>();

builder.Services.AddSingleton<SegundaOportunidad.Services.RedisService>();
builder.Services.AddSingleton<SegundaOportunidad.Services.RabbitMqPublisher>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<SegundaOportunidad.Services.PieSocketService>();
builder.Services.AddHostedService<SegundaOportunidad.Services.ChatBackgroundService>();
builder.Services.AddControllersWithViews(options => options.Filters.Add<VerifiedAccessFilter>())
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddAccountSecurity(builder.Configuration);
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await SegundaOportunidad.Data.DataSeeder.SeedDataAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Un error ocurrió sembrando la base de datos.");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseRateLimiter();
app.UseMiddleware<SegundaOportunidad.Services.SuspensionMiddleware>();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();