using Microsoft.EntityFrameworkCore;

namespace SegundaOportunidad.Data;

public static class SampleStartupInitializer
{
    public static async Task InitializeAsync(IServiceProvider services,IWebHostEnvironment environment,IConfiguration config,CancellationToken ct=default)
    {
        if(!config.GetValue<bool>("SampleData:InitializeOnStartup"))return;
        if(!environment.IsEnvironment("Demo")&&!environment.IsDevelopment())throw new InvalidOperationException("La inicialización del catálogo de presentación no se permite en Production.");
        var db=services.GetRequiredService<ApplicationDbContext>();
        // No se vuelve a tocar el catálogo ni sus cuentas cuando ya están preparados.
        var users=await db.Users.CountAsync(u=>u.Id.StartsWith("demo-ml-user-"),ct);
        var articles=await db.Articulos.IgnoreQueryFilters().CountAsync(a=>a.Id.StartsWith("demo-ml-art-"),ct);
        if(users==10&&articles==48)return;
        if(string.IsNullOrWhiteSpace(config["SampleData:CredentialsJson"]))throw new InvalidOperationException("Configura SampleData:CredentialsJson como secreto de Render para preparar las cuentas; nunca se publican contraseñas en Git.");
        await services.GetRequiredService<DemoCatalogSeeder>().SeedAsync(ct);
    }
}
