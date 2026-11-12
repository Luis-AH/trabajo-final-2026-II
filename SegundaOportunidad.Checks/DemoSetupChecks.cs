using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;

internal static class DemoSetupChecks
{
    public static async Task Run(string path)
    {
        var full=ValidateDemoPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions {EnvironmentName="Demo",ContentRootPath=Path.GetFullPath("SegundaOportunidad")});
        builder.Logging.ClearProviders();
        builder.Services.AddDbContext<ApplicationDbContext>(o=>o.UseSqlite("Data Source="+full));
        builder.Services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
        builder.Services.AddScoped<DemoCatalogSeeder>();
        await using var app=builder.Build();using var scope=app.Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        var seeder=scope.ServiceProvider.GetRequiredService<DemoCatalogSeeder>();
        var result=await seeder.SeedAsync();
        var passwordPath=Path.Combine(Path.GetDirectoryName(full)!,"accounts.private.json");
        var credentials=File.Exists(passwordPath)?JsonSerializer.Deserialize<List<DemoAccount>>(await File.ReadAllTextAsync(passwordPath))??[]:[];
        credentials.AddRange(result.CuentasNuevas);
        await File.WriteAllTextAsync(passwordPath,JsonSerializer.Serialize(credentials,new JsonSerializerOptions {WriteIndented=true}));
        var again=await seeder.SeedAsync();
        if(again.NuevasCuentas!=0||again.NuevasPublicaciones!=0||again.NuevosIntercambios!=0)throw new Exception("El seeding no es idempotente.");
        var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        foreach(var credential in result.CuentasNuevas)
        {
            var user=await users.FindByEmailAsync(credential.Email);
            if(user==null||!await users.CheckPasswordAsync(user,credential.Password))throw new Exception("No se validó Identity para la cuenta DEMO.");
        }
        if(await db.Users.CountAsync(u=>u.Id.StartsWith("demo-ml-user-"))!=10 || await db.Articulos.IgnoreQueryFilters().CountAsync(a=>a.Id.StartsWith("demo-ml-art-"))!=48)throw new Exception("Cantidad DEMO incorrecta.");
        Console.WriteLine("OK DEMO: 10 cuentas ficticias, 48 publicaciones y 6 solicitudes. Seeder idempotente y contraseñas verificadas por Identity.");
        Console.WriteLine("Base DEMO: "+full);
        Console.WriteLine("Credenciales privadas, sin mostrar sus valores: "+passwordPath);
    }
    internal static string ValidateDemoPath(string path)
    {
        var full=Path.GetFullPath(path);
        var comparison=OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
        var real=Path.GetFullPath("SegundaOportunidad/app.db");
        var demo=Path.GetFullPath(".demo")+Path.DirectorySeparatorChar;
        var temporary=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        if(full.Equals(real,comparison)||(!full.StartsWith(demo,comparison)&&!full.StartsWith(temporary,comparison)))
            throw new InvalidOperationException("Prepara la base únicamente dentro de .demo o de un directorio temporal. No se migró ni modificó el destino rechazado.");
        return full;
    }
}
