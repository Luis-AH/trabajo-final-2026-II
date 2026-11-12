using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Models;

namespace SegundaOportunidad.Data;

public static class SampleStartupInitializer
{
    public static async Task InitializeAsync(
        IServiceProvider services,
        IWebHostEnvironment environment,
        IConfiguration config,
        CancellationToken ct = default)
    {
        if (!config.GetValue<bool>("SampleData:InitializeOnStartup"))
        {
            return;
        }

        if (!environment.IsEnvironment("Demo") && !environment.IsDevelopment())
        {
            throw new InvalidOperationException("La inicialización del catálogo de presentación no se permite en Production.");
        }

        var db = services.GetRequiredService<ApplicationDbContext>();
        var userCount = await db.Users.CountAsync(user => user.Id.StartsWith("demo-ml-user-"), ct);
        var articleCount = await db.Articulos.IgnoreQueryFilters()
            .CountAsync(article => article.Id.StartsWith("demo-ml-art-"), ct);

        if (userCount == 10 && articleCount == 48)
        {
            var passwords = SampleProfiles.ResolvePasswords(config, environment);
            if (passwords == null)
            {
                return;
            }

            var manager = services.GetRequiredService<UserManager<ApplicationUser>>();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var repaired = 0;

            for (var number = 1; number <= 10; number++)
            {
                var profile = SampleProfiles.Accounts[number - 1];
                var user = await manager.FindByIdAsync($"demo-ml-user-{number:00}")
                    ?? throw new InvalidOperationException("Falta una cuenta del catálogo de presentación.");

                if (!string.Equals(user.Email, profile.Email, StringComparison.OrdinalIgnoreCase)
                    && user.Email != $"demo{number:00}@example.invalid")
                {
                    throw new InvalidOperationException("Una cuenta existente no coincide con el catálogo de presentación.");
                }

                if (user.Email != profile.Email || user.UserName != profile.Email)
                {
                    user.Email = profile.Email;
                    user.UserName = profile.Email;
                    if (!(await manager.UpdateAsync(user)).Succeeded)
                    {
                        throw new InvalidOperationException("No se pudo actualizar el acceso de presentación.");
                    }
                }

                if (await manager.CheckPasswordAsync(user, passwords[profile.Email]))
                {
                    continue;
                }

                if (await manager.HasPasswordAsync(user) && !(await manager.RemovePasswordAsync(user)).Succeeded)
                {
                    throw new InvalidOperationException("No se pudo sincronizar el acceso de presentación.");
                }

                if (!(await manager.AddPasswordAsync(user, passwords[profile.Email])).Succeeded)
                {
                    throw new InvalidOperationException("La contraseña configurada no cumple los requisitos de Identity.");
                }

                repaired++;
            }

            await transaction.CommitAsync(ct);
            services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("SampleStartupInitializer")
                .LogInformation("Se sincronizaron {Count} contraseñas del catálogo de presentación.", repaired);
            return;
        }

        await services.GetRequiredService<DemoCatalogSeeder>().SeedAsync(ct);
        if (SampleProfiles.ResolvePasswords(config, environment) == null)
        {
            services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("SampleStartupInitializer")
                .LogInformation("Catálogo y recomendaciones listos. Configura SampleData__CredentialsJson para utilizar las contraseñas conocidas de presentación.");
        }
    }
}
