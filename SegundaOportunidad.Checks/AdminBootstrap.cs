using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;

internal static class AdminBootstrap
{
    public static async Task Run(string path)
    {
        await DatabaseUpgrade.Run(path); // Respaldo SQLite antes de cambiar asignaciones.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = Path.GetFullPath(path) }.ToString()));
        services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var admin = await users.FindByIdAsync("admin-fixed-guid-001");
        if (admin?.Email != "admin@segundaoportunidad.com") throw new Exception("Cuenta administrativa prevista no encontrada. No se promueve otra cuenta.");
        if (!await roles.RoleExistsAsync("Admin") && !(await roles.CreateAsync(new IdentityRole("Admin"))).Succeeded) throw new Exception("No se pudo crear rol");
        if (!await users.IsInRoleAsync(admin, "Admin") && !(await users.AddToRoleAsync(admin, "Admin")).Succeeded) throw new Exception("No se pudo asignar rol");
        Console.WriteLine("OK: rol Admin asignado a la cuenta prevista, sin cambiar contraseña ni crear usuarios.");
    }
}
