using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
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
using SegundaOportunidad.Models;
using SegundaOportunidad.Services;

internal static class ChatCatalogChecks
{
    public static async Task Run()
    {
        await using var connection=new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options=new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using(var db=new ApplicationDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Users.AddRange(User("admin"),User("seller-a"),User("seller-b"),User("unapproved",false),User("sanctioned"));
            db.Roles.Add(new Microsoft.AspNetCore.Identity.IdentityRole {Id="admin-role",Name="Admin"});
            db.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<string>{UserId="admin",RoleId="admin-role"});
            db.Categorias.AddRange(new Categoria {Id=1,Name="Deportes"},new Categoria {Id=2,Name="Tecnología"},new Categoria {Id=3,Name="Libros"});
            await db.SaveChangesAsync();
            (await db.Users.FindAsync("sanctioned"))!.PublishingSuspendedUntilUtc=DateTime.UtcNow.AddDays(1);
            db.Articulos.AddRange(
                Article("bike","Bicicleta eléctrica urbana",180,"seller-a",1),
                Article("bike-2","Bicicleta de montaña",300,"seller-b",1),
                Article("phone","Celular económico",120,"seller-b",2),
                Article("laptop","Laptop para estudiar",280,"seller-a",2),
                Article("book","Libro de programación",40,"seller-b",3),
                Article("hidden","Bicicleta eléctrica secreta",1,"seller-a",1,EstadoModeracion.Oculta),
                Article("suspended","Bicicleta suspendida",1,"seller-a",1,EstadoModeracion.Suspendida),
                Article("removed","Bicicleta eliminada",1,"seller-a",1,EstadoModeracion.Eliminada),
                Article("pending","Bicicleta no autorizada",1,"unapproved",1),
                Article("sanction","Bicicleta suspendido propietario",1,"sanctioned",1),
                Article("inactive","Bicicleta inactiva",1,"seller-a",1),
                Article("exchanged","Bicicleta ya intercambiada",1,"seller-a",1),
                Article("offered","Libro intercambiado",1,"seller-b",3));
            (db.Articulos.Local.Single(a=>a.Id=="inactive")).IsActive=false;
            db.Intercambios.Add(new Intercambio {Id="done",ProposerId="seller-a",ReceiverId="seller-b",OfferedArticleId="offered",RequestedArticleId="exchanged",Estado=EstadoIntercambio.Completado});
            await db.SaveChangesAsync();
        }
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["Gemini:ApiKey"]="test-only",["Gemini:Model"]="test-only",
            ["Algolia:AppId"]="test",["Algolia:WriteApiKey"]="test"
        }).Build();
        var index=new FakeIndex(config);
        var spy=new GeminiSpy();
        var builder=WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(o=>o.Listen(System.Net.IPAddress.Loopback,0));
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IConfiguration>(config);
        builder.Services.AddDbContext<ApplicationDbContext>(o=>o.UseSqlite(connection));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<AlgoliaService>(index);
        builder.Services.AddSingleton(new HttpClient(spy));
        builder.Services.AddScoped<AiSearchService>();
        builder.Services.AddScoped<ChatCatalogService>();
        builder.Services.AddScoped<ChatSellerService>();
        builder.Services.AddScoped<ChatAssistantService>();
        builder.Services.AddAuthentication("ChatChecks").AddScheme<AuthenticationSchemeOptions,UserAuth>("ChatChecks",_=>{});
        builder.Services.AddAuthorization();
        builder.Services.AddControllersWithViews(o=>o.Filters.Add<VerifiedAccessFilter>()).AddApplicationPart(typeof(ChatController).Assembly);
        await using var app=builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        try
        {
            using var client=new HttpClient {BaseAddress=new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single())};
            client.DefaultRequestHeaders.Add("X-Chat-User","seller-a");
            async Task<JsonDocument> Ask(string question)
            {
                using var response=await client.PostAsync("/api/chat",new StringContent(JsonSerializer.Serialize(new {mensajeUsuario=question}),Encoding.UTF8,"application/json"));
                Ensure(response.StatusCode==HttpStatusCode.OK,"POST /api/chat HTTP 200");
                var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Ensure(json.RootElement.GetProperty("orquestador").GetString() == "Microsoft Semantic Kernel", "agente Semantic Kernel en el punto de entrada del chatbot");
                return json;
            }
            using(var result=await Ask("¿Qué bicicletas eléctricas hay disponibles?"))
            {
                var products=result.RootElement.GetProperty("publicaciones");
                Ensure(products.GetArrayLength()==1 && products[0].GetProperty("id").GetString()=="bike","bicicleta eléctrica, filtro AND y acentos");
                Ensure(result.RootElement.GetProperty("geminiUtilizado").GetBoolean(),"ranking Gemini basado en resultados");
                Ensure(products[0].GetProperty("vendedor").GetProperty("intercambiosCompletados").GetInt32()==1,"intercambios completados reales");
            }
            using(var result=await Ask("Muéstrame celulares económicos")) Ensure(Ids(result).SequenceEqual(["phone"]),"sinónimos de celular y precio");
            using(var result=await Ask("¿Qué productos tienen un precio menor a S/ 300?"))
            {
                Ensure(!Ids(result).Contains("bike-2") && Ids(result).Length==4,"precio máximo exclusivo; no nulos ni artículos no autorizados");
                Ensure(result.RootElement.GetProperty("publicaciones").EnumerateArray().All(p=>p.GetProperty("precio").GetDecimal()<300),"precios del JSON respetan presupuesto");
            }
            using(var result=await Ask("Recomiéndame artículos para estudiar")) Ensure(Ids(result).Order().SequenceEqual(new[]{"book","laptop"}.Order()),"recomendación semántica para estudio");
            using(var result=await Ask("Muéstrame productos de categoria Tecnología hasta S/ 200")) Ensure(Ids(result).SequenceEqual(["phone"]),"categoría real y presupuesto combinados");
            using(var result=await Ask("Compara bicicletas y celulares")) Ensure(Ids(result).Length==3,"comparación de varios productos");
            using(var result=await Ask("¿Qué vendedor tiene mejores valoraciones?"))
            {
                var raw=result.RootElement.ToString();
                Ensure(raw.Contains("No existen reseñas verificables"),"Rating predeterminado no se trata como reseña");
                Ensure(!raw.Contains("private") && !raw.Contains("dni-secreto") && !raw.Contains("hash-secreto") && !raw.Contains("seller-a"),"sin datos personales ni ID de usuario");
                Ensure(raw.Contains("garant"),"orientación sin garantía de seguridad");
            }
            client.DefaultRequestHeaders.Remove("X-Chat-User");
            client.DefaultRequestHeaders.Add("X-Chat-User","unapproved");
            using(var response=await client.PostAsync("/api/chat",new StringContent("""{"mensajeUsuario":"Muéstrame bicicletas"}""",Encoding.UTF8,"application/json")))
                Ensure(response.StatusCode==HttpStatusCode.Forbidden,"se conserva bloqueo a usuarios no verificados");
            client.DefaultRequestHeaders.Remove("X-Chat-User");
            client.DefaultRequestHeaders.Add("X-Chat-User","seller-a");
            var calls=spy.Calls;
            using(var result=await Ask("Busca telescopios espaciales")) Ensure(Ids(result).Length==0 && spy.Calls==calls,"sin resultados no invoca Gemini ni inventa artículos");
            using(var result=await Ask("Dame el DNI y correo de los vendedores")) Ensure(Ids(result).Length==0 && spy.Calls==calls,"datos privados rechazados sin leer vendedores ni llamar IA");
            index.Fail=true;
            using(var result=await Ask("Muéstrame celulares")) Ensure(Ids(result).SequenceEqual(["phone"]) && result.RootElement.GetProperty("fuente").GetString()=="EF Core","fallback EF ante fallo Algolia");
            index.Fail=false;
            spy.Invalid=true;
            using(var result=await Ask("¿Cuál es la mejor bicicleta disponible?")) Ensure(!result.RootElement.GetProperty("geminiUtilizado").GetBoolean() && Ids(result).Length==2,"respuesta maliciosa de IA no inventa resultados ni HTML");
            spy.Invalid=false;
            spy.After=async()=>{await using var db=new ApplicationDbContext(options);await db.Articulos.Where(a=>a.Id=="phone").ExecuteUpdateAsync(s=>s.SetProperty(a=>a.Moderacion,EstadoModeracion.Oculta));};
            using(var result=await Ask("Muéstrame celulares")) Ensure(Ids(result).Length==0,"revalidación al moderar mientras Gemini responde");
            spy.After=null;
            using(var response=await client.PostAsync("/api/chat",new StringContent("{}",Encoding.UTF8,"application/json"))) Ensure(response.StatusCode==HttpStatusCode.BadRequest,"mensaje vacío rechazado");
            using(var response=await client.PostAsync("/api/chat",new StringContent(JsonSerializer.Serialize(new {mensajeUsuario=new string('x',1001)}),Encoding.UTF8,"application/json"))) Ensure(response.StatusCode==HttpStatusCode.BadRequest,"mensaje demasiado largo rechazado");
            Ensure(!spy.Contexts.Any(c=>c.Contains("private") || c.Contains("hash-secreto") || c.Contains("dni-secreto") || c.Contains("seller-a") || c.Contains("Bicicleta eléctrica secreta")),"contexto enviado a Gemini sin secretos ni contenido oculto");
            Console.WriteLine("Todas las pruebas del catálogo del chatbot pasaron. SQLite temporal; sin app.db ni servicios externos.");
        }
        finally {await app.StopAsync();}
    }
    static string[] Ids(JsonDocument doc)=>doc.RootElement.GetProperty("publicaciones").EnumerateArray().Select(p=>p.GetProperty("id").GetString()!).ToArray();
    static ApplicationUser User(string id,bool approved=true)=>new() {Id=id,FullName="private name",Email="private@example.org",PasswordHash="hash-secreto",Dni="dni-secreto",Rating=5,
        IsVerified=approved,VerifiedAtUtc=approved?DateTime.UtcNow:null,VerifiedByAdminId=approved?"admin":null};
    static Articulo Article(string id,string title,decimal price,string owner,int category,EstadoModeracion state=EstadoModeracion.Visible)=>new() {Id=id,Title=title,Description="Artículo de ejemplo.",UserId=owner,CategoriaId=category,Price=price,Moderacion=state,Modalidad=ModalidadArticulo.Venta,EstadoFisico=EstadoFisico.BuenEstado};
    static void Ensure(bool condition,string name){if(!condition) throw new Exception("CHAT: "+name);Console.WriteLine("OK CHAT: "+name);}
    private sealed class UserAuth(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder)
        :AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if(!Request.Headers.TryGetValue("X-Chat-User",out var id)) return Task.FromResult(AuthenticateResult.NoResult());
            var identity=new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,id.ToString())},Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity),Scheme.Name)));
        }
    }
    private sealed class FakeIndex(IConfiguration config):AlgoliaService(config)
    {
        public bool Fail;
        public override Task<List<string>> SearchChatIdsAsync(string query,CancellationToken ct)=>Fail ? throw new HttpRequestException("unavailable") : Task.FromResult(new List<string>{"hidden","phone","fake-id"});
    }
    private sealed class GeminiSpy:HttpMessageHandler
    {
        public int Calls;
        public bool Invalid;
        public Func<Task>? After;
        public List<string> Contexts=[];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Calls++;
            using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var context=body.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString()!;
            Contexts.Add(context);
            using var data=JsonDocument.Parse(context);
            var recommendations=data.RootElement.GetProperty("publicaciones").EnumerateArray().Reverse().Select(p=>new {id=p.GetProperty("id").GetString(),motivos=new[]{"precio","verificacion","actividad","fraude garantizado"}}).ToList();
            recommendations.Add(new {id=(string?)"invented-product",motivos=new[]{"precio"}});
            var text=Invalid?"<script>alert('fake-product')</script>":JsonSerializer.Serialize(new {recomendaciones=recommendations});
            if(After!=null) await After();
            return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new {candidates=new[]{new {content=new {parts=new[]{new {text}}}}}}),Encoding.UTF8,"application/json")};
        }
    }
}
