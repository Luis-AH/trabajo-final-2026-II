using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;
using SegundaOportunidad.Services;

internal static class GuardLiveChecks
{
    // Solo se ejecuta explícitamente: consume cuota de Groq. Nunca imprime la clave.
    public static async Task Run()
    {
        var config = new ConfigurationBuilder().AddUserSecrets(typeof(ApplicationDbContext).Assembly, optional: true).AddEnvironmentVariables().Build();
        var settings = new AdminGuardOptions();
        config.GetSection("AdminGuard").Bind(settings);
        if (string.IsNullOrWhiteSpace(settings.ApiKey) || string.IsNullOrWhiteSpace(settings.Model)) throw new InvalidOperationException("Falta configurar clave o modelo de AdminGuard.");
        var options = Options.Create(settings);
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(45) };
        using var imageHttp = new HttpClient(AdminGuardImageReader.Handler()) { Timeout = TimeSpan.FromSeconds(15) };
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.MigrateAsync();
        db.Users.Add(new ApplicationUser { Id = "test-admin", UserName = "test-admin" });
        db.Categorias.Add(new Categoria { Id = 1, Name = "Deportes" });
        db.Articulos.Add(new Articulo { Id = "test-bicicleta", UserId = "test-admin", CategoriaId = 1,
            Title = "Bicicleta de ejemplo", Description = "Fotografía pública para comprobar el análisis visual; estado y propiedad no verificados.",
            ImageUrl = "https://images.unsplash.com/photo-1485965120184-e220f721d03e?auto=format&fit=crop&w=320&q=70" });
        await db.SaveChangesAsync();
        var client = new AdminGuardClient(http,options);
        var service = new AdminGuardService(db,client,new AdminGuardImageReader(imageHttp,options),TimeProvider.System,new AdminGuardGate());
        var id = await service.ConsultarAsync("test-admin",new() { Tipo=TipoConsultaAdminGuard.Publicacion,ObjetivoId="test-bicicleta",
            Pregunta="Describe brevemente qué ves en la fotografía y si coincide con el título. No determines autenticidad ni culpabilidad." },default);
        var analysis = await db.AnalisisAdminGuard.SingleAsync(a => a.Id == id);
        if (!analysis.Completado || analysis.ImagenSha256?.Length != 64 || string.IsNullOrWhiteSpace(analysis.Resultado)) throw new Exception("Análisis real incompleto.");
        Console.WriteLine("OK GROQ REAL: imagen descargada mediante lector seguro, SHA256 calculado, análisis multimodal completado y auditoría temporal persistida.");
        Console.WriteLine("Modelo: " + settings.Model);
        Console.WriteLine("Respuesta: " + analysis.Resultado);
        Console.WriteLine("No se modificó app.db ni se aplicaron sanciones.");
    }
}
