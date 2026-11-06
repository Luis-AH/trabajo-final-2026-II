using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SegundaOportunidad.Controllers;
using SegundaOportunidad.Data;
using SegundaOportunidad.Services;

internal static class GeminiLiveChecks
{
    // Ejecución optativa, consume cuota. Base existente solo en modo lectura; sin app principal ni servicios de mensajería.
    public static async Task Run(string? catalogPath=null)
    {
        var config=new ConfigurationBuilder().AddUserSecrets(typeof(ChatController).Assembly,optional:true).AddEnvironmentVariables().Build();
        // Esta comprobación usa EF local y no envía términos al índice compartido.
        config["Algolia:AppId"]=null;
        await using var connection=new SqliteConnection(catalogPath==null ? "Data Source=:memory:" :
            new SqliteConnectionStringBuilder {DataSource=Path.GetFullPath(catalogPath),Mode=SqliteOpenMode.ReadOnly}.ToString());
        await connection.OpenAsync();
        if(catalogPath==null)
        {
            await using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
        }
        var observer=new GoogleObserver();
        var builder=WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(o=>o.Listen(System.Net.IPAddress.Loopback,0));
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IConfiguration>(config);
        builder.Services.AddDbContext<ApplicationDbContext>(o=>o.UseSqlite(connection));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<AlgoliaService>();
        builder.Services.AddSingleton(new HttpClient(observer){Timeout=TimeSpan.FromSeconds(25)});
        builder.Services.AddScoped<AiSearchService>();
        builder.Services.AddScoped<ChatCatalogService>();
        builder.Services.AddScoped<ChatSellerService>();
        builder.Services.AddScoped<ChatAssistantService>();
        builder.Services.AddControllers().AddApplicationPart(typeof(ChatController).Assembly);
        await using var app=builder.Build();
        app.MapControllers();
        await app.StartAsync();
        try
        {
            var address=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var browser=new HttpClient {BaseAddress=new Uri(address),Timeout=TimeSpan.FromSeconds(170)};
            var question=catalogPath==null ? "Responde solamente: OK_SEGUNDA" : "Recomiéndame artículos para estudiar";
            using var response=await browser.PostAsync("/api/chat",new StringContent(JsonSerializer.Serialize(new {mensajeUsuario=question}),Encoding.UTF8,"application/json"));
            if(!response.IsSuccessStatusCode) throw new Exception("El host de prueba /api/chat devolvió HTTP "+(int)response.StatusCode);
            using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var reply=json.RootElement.GetProperty("respuesta").GetString();
            Console.WriteLine("Modelo configurado: "+config["Gemini:Model"]);
            Console.WriteLine("POST /api/chat HTTP "+(int)response.StatusCode);
            if(response.StatusCode!=HttpStatusCode.OK || observer.GeneratedText==null) throw new Exception("No se recibió texto generado por Google.");
            if(catalogPath==null)
            {
                if(reply!=observer.GeneratedText) throw new Exception("El texto de Google no coincide con respuesta.");
                Console.WriteLine("OK REAL: texto Google coincide con respuesta del chatbot: "+reply);
            }
            else
            {
                if(!json.RootElement.GetProperty("geminiUtilizado").GetBoolean()) throw new Exception("Gemini no devolvió ranking válido para el catálogo.");
                var products=json.RootElement.GetProperty("publicaciones");
                if(products.GetArrayLength()==0) throw new Exception("No hay publicaciones reales devueltas.");
                foreach(var product in products.EnumerateArray())
                {
                    var id=product.GetProperty("id").GetString()!;
                    await using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
                    if(!await db.Articulos.AnyAsync(a=>a.Id==id && a.IsActive)) throw new Exception("Artículo devuelto no disponible en base local.");
                    if(product.GetProperty("enlace").GetString()!="/Articulos/Details/"+Uri.EscapeDataString(id)) throw new Exception("Enlace no corresponde al artículo.");
                    Console.WriteLine("Publicación real: "+product.GetProperty("titulo").GetString()+" | "+product.GetProperty("enlace").GetString());
                }
                Console.WriteLine("OK REAL: Gemini ordenó el catálogo local; fichas y enlaces verificados contra SQLite en modo lectura.");
            }
            Console.WriteLine("Host MVC temporal sin escrituras ni modificaciones de autenticación en la aplicación principal.");
        }
        finally {await app.StopAsync();}
    }
    private sealed class GoogleObserver():DelegatingHandler(new HttpClientHandler {AllowAutoRedirect=false})
    {
        public string? GeneratedText {get;private set;}
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            var response=await base.SendAsync(request,token);
            Console.WriteLine("Google generateContent HTTP "+(int)response.StatusCode);
            if(response.IsSuccessStatusCode)
            {
                using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
                if(doc.RootElement.TryGetProperty("candidates",out var candidates) && candidates.GetArrayLength()>0 &&
                    candidates[0].TryGetProperty("content",out var content) && content.TryGetProperty("parts",out var parts))
                    GeneratedText=string.Concat(parts.EnumerateArray().Where(p=>p.TryGetProperty("text",out _)).Select(p=>p.GetProperty("text").GetString()));
            }
            return response;
        }
    }
}
