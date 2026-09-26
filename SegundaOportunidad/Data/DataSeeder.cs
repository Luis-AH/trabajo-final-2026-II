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

            context.Database.EnsureCreated();

            // 1. Crear Categorías
            if (!context.Categorias.Any())
            {
                context.Categorias.AddRange(
                    new Categoria { Name = "Ropa", Icon = "👕" },
                    new Categoria { Name = "Muebles", Icon = "🪑" },
                    new Categoria { Name = "Electrónica", Icon = "💻" },
                    new Categoria { Name = "Libros", Icon = "📚" },
                    new Categoria { Name = "Deportes", Icon = "⚽" }
                );
                await context.SaveChangesAsync();
            }

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
                    Dni = "87654321",
                    IsVerified = true,
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(user, "User123!");
            }

            // 3. Crear Artículos de Prueba
            if (!context.Articulos.Any())
            {
                var catDeporte = context.Categorias.FirstOrDefault(c => c.Name == "Deportes")?.Id ?? 1;
                
                context.Articulos.Add(new Articulo
                {
                    Id = "articulo-fijo-001",
                    Title = "Bicicleta Montañera Trek Vintage",
                    Description = "En buen estado, ideal para paseos por la ciudad.",
                    Modalidad = ModalidadArticulo.Venta,
                    EstadoFisico = EstadoFisico.BuenEstado,
                    Price = 180,
                    CategoriaId = catDeporte,
                    UserId = adminUserId
                });

                await context.SaveChangesAsync();
            }
        }
    }
}
