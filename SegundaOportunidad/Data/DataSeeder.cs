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
                var algolia = serviceProvider.GetRequiredService<SegundaOportunidad.Services.AlgoliaService>();
                
                var catDeporte = context.Categorias.FirstOrDefault(c => c.Name == "Deportes")?.Id ?? 1;
                var catElectronica = context.Categorias.FirstOrDefault(c => c.Name == "Electrónica")?.Id ?? 2;
                var catRopa = context.Categorias.FirstOrDefault(c => c.Name == "Ropa")?.Id ?? 3;
                var catLibros = context.Categorias.FirstOrDefault(c => c.Name == "Libros")?.Id ?? 4;
                var catMuebles = context.Categorias.FirstOrDefault(c => c.Name == "Muebles")?.Id ?? 5;
                
                var articulos = new List<Articulo>
                {
                    new Articulo { Id = "articulo-fijo-001", Title = "Bicicleta Montañera Trek Vintage", Description = "En buen estado, ideal para paseos por la ciudad.", Modalidad = ModalidadArticulo.Venta, EstadoFisico = EstadoFisico.BuenEstado, Price = 180, CategoriaId = catDeporte, UserId = adminUserId, ImageUrl = "https://images.unsplash.com/photo-1485965120184-e220f721d03e?auto=format&fit=crop&w=500&q=80" },
                    new Articulo { Id = "articulo-fijo-002", Title = "Smartphone Samsung S20", Description = "Pantalla intacta, batería al 80%.", Modalidad = ModalidadArticulo.Intercambio, EstadoFisico = EstadoFisico.Aceptable, Price = 0, CategoriaId = catElectronica, UserId = normalUserId, ImageUrl = "https://images.unsplash.com/photo-1598327105666-5b89351cb315?auto=format&fit=crop&w=500&q=80" },
                    new Articulo { Id = "articulo-fijo-003", Title = "Chaqueta de Cuero Negra", Description = "Talla M, casi sin uso.", Modalidad = ModalidadArticulo.Venta, EstadoFisico = EstadoFisico.ComoNuevo, Price = 120, CategoriaId = catRopa, UserId = adminUserId, ImageUrl = "https://images.unsplash.com/photo-1551028719-00167b16eac5?auto=format&fit=crop&w=500&q=80" },
                    new Articulo { Id = "articulo-fijo-004", Title = "Libro Cien Años de Soledad", Description = "Edición de tapa dura.", Modalidad = ModalidadArticulo.Donacion, EstadoFisico = EstadoFisico.BuenEstado, Price = 0, CategoriaId = catLibros, UserId = normalUserId, ImageUrl = "https://images.unsplash.com/photo-1544947950-fa07a98d237f?auto=format&fit=crop&w=500&q=80" },
                    new Articulo { Id = "articulo-fijo-005", Title = "Sofá Cama 3 Plazas", Description = "Color gris, cómodo y funcional.", Modalidad = ModalidadArticulo.Venta, EstadoFisico = EstadoFisico.BuenEstado, Price = 350, CategoriaId = catMuebles, UserId = adminUserId, ImageUrl = "https://images.unsplash.com/photo-1555041469-a586c61ea9bc?auto=format&fit=crop&w=500&q=80" }
                };

                context.Articulos.AddRange(articulos);
                await context.SaveChangesAsync();

                // Sincronizar con Algolia
                foreach (var articulo in articulos)
                {
                    try
                    {
                        await algolia.IndexArticuloAsync(articulo);
                    }
                    catch (Exception) { /* Ignorar errores de red si Algolia no responde */ }
                }
            }
        }
    }
}
