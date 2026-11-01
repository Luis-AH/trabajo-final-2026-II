using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using SegundaOportunidad.Models;

namespace SegundaOportunidad.Data
{
    public static class DataSeeder
    {
        public static async Task SeedDataAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            await context.Database.MigrateAsync();

            await CategoryCatalog.EnsureAsync(context);

            // 2. Crear Usuarios con GUIDs fijos (para evitar huérfanos en RabbitMQ por discos efímeros)
            var adminUserId = "admin-fixed-guid-001";
            var normalUserId = "user-fixed-guid-002";

            if (await userManager.FindByIdAsync(adminUserId) == null)
            {
                var admin = new ApplicationUser
                {
                    Id = adminUserId,
                    UserName = "admin@segundaoportunidad.com",
                    Email = "admin@segundaoportunidad.com",
                    FullName = "Admin Principal",
                    Dni = "12345678",
                    IsVerified = true,
                    EmailConfirmed = true,
                    Co2Saved = 14.5m,
                    ObjectsReused = 10,
                    FamiliesHelped = 3
                };
                await userManager.CreateAsync(admin, "Admin123!");
            }

            if (await userManager.FindByIdAsync(normalUserId) == null)
            {
                var user = new ApplicationUser
                {
                    Id = normalUserId,
                    UserName = "user@segundaoportunidad.com",
                    Email = "user@segundaoportunidad.com",
                    FullName = "Usuario Común",
                    Dni = null,
                    IsVerified = false,
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(user, "User123!");
            }

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roleManager.RoleExistsAsync("Admin"))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole("Admin"));
                if (!roleResult.Succeeded)
                    throw new InvalidOperationException("No se pudo crear el rol Admin.");
            }
            var administrador = await userManager.FindByIdAsync(adminUserId);
            if (administrador != null && !await userManager.IsInRoleAsync(administrador, "Admin"))
            {
                var roleResult = await userManager.AddToRoleAsync(administrador, "Admin");
                if (!roleResult.Succeeded)
                    throw new InvalidOperationException("No se pudo asignar el rol Admin.");
            }

            // Las publicaciones se crean desde la aplicación; no se reinsertan datos de demostración.
        }
    }
}
