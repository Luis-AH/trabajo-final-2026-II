using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;
using SegundaOportunidad.Services;

internal static class GuardChecks
{
    static void Check(bool condition, string label) { if (!condition) throw new Exception(label); Console.WriteLine("OK GUARD: " + label); }
    internal sealed class TestClient : IAdminGuardClient
    {
        public bool Configurado => true;
        public string Modelo => "modelo-simulado-pruebas";
        public string LastData = "";
        public bool Fail;
        public Task<string> ConsultarAsync(string pregunta,string datos,byte[]? image,string? mime,CancellationToken ct)
        {
            LastData = datos;
            if (Fail) throw new HttpRequestException("Fallo simulado");
            return Task.FromResult("Observación simulada: requiere verificación humana. <script>alert(1)</script>");
        }
    }
    internal sealed class TestImage : IAdminGuardImageReader
    {
        public Task<(byte[] Bytes,string Mime)> LeerAsync(string url,CancellationToken ct) => Task.FromResult((new byte[] {137,80,78,71,13,10,26,10},"image/png"));
    }
    public static async Task Run(HttpClient admin,HttpClient member,IServiceProvider services)
    {
        Check((await member.GetAsync("/AdminGuard")).StatusCode == HttpStatusCode.Redirect,"Rol común no accede a IA");
        var page = await admin.GetAsync("/AdminGuard");
        Check(page.IsSuccessStatusCode,"Panel Razor renderizado");
        var token = WebUtility.HtmlDecode(Regex.Match(await page.Content.ReadAsStringAsync(),"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        var direct=await admin.GetAsync("/AdminGuard/Consultar?tipo=Publicacion&objetivoId=art");
        Check(direct.StatusCode==HttpStatusCode.Redirect && direct.Headers.Location!.ToString().Contains("AdminGuard"),"Acceso GET a Consultar redirige al panel");
        var invalid=await admin.PostAsync("/AdminGuard/Consultar",new FormUrlEncodedContent(new Dictionary<string,string> { ["Tipo"]="Publicacion",["ObjetivoId"]="no-existe",["Pregunta"]="Analizar una publicación",["__RequestVerificationToken"]=token }));
        Check(invalid.IsSuccessStatusCode && (await invalid.Content.ReadAsStringAsync()).Contains("El elemento no"),"Referencia inexistente muestra error en panel, no 404");
        Check((await admin.PostAsync("/AdminGuard/Consultar",new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode == HttpStatusCode.BadRequest,"IA exige antiforgery");
        var sent = await admin.PostAsync("/AdminGuard/Consultar",new FormUrlEncodedContent(new Dictionary<string,string> { ["Tipo"]="Estadisticas", ["Pregunta"]="Qué revisión conviene priorizar", ["__RequestVerificationToken"]=token }));
        Check(sent.StatusCode == HttpStatusCode.Redirect,"Consulta HTTP con proveedor simulado");
        var result = await admin.GetAsync(sent.Headers.Location);
        var html = await result.Content.ReadAsStringAsync();
        Check(result.IsSuccessStatusCode && !html.Contains("<script>alert(1)</script>") && html.Contains("&lt;script&gt;"),"Respuesta IA se codifica como texto");
        Check((await member.GetAsync(sent.Headers.Location)).StatusCode == HttpStatusCode.Redirect,"Usuario común no accede al análisis");
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var fake = (TestClient)scope.ServiceProvider.GetRequiredService<IAdminGuardClient>();
        var service = scope.ServiceProvider.GetRequiredService<AdminGuardService>();
        var previousSanctions = await db.SancionesUsuarios.CountAsync();
        var article = await db.Articulos.IgnoreQueryFilters().SingleAsync(a => a.Id == "art");
        article.ImageUrl = "https://images.unsplash.com/test";
        article.Description = "contacto private@example.com password=SECRETTEST teléfono 999888777";
        db.Articulos.Add(new Articulo { Id="twin", UserId="member", CategoriaId=1, ImageUrl=article.ImageUrl, Title="Otro artículo" });
        await db.SaveChangesAsync();
        var selected=await admin.GetStringAsync("/AdminGuard/Analizar?id=art");
        Check(selected.Contains("name=\"ObjetivoId\"") && selected.Contains("value=\"art\""),"Publicación seleccionada en página de análisis dedicada");
        var publication=await admin.PostAsync("/AdminGuard/Consultar",new FormUrlEncodedContent(new Dictionary<string,string> { ["Tipo"]="Publicacion",["ObjetivoId"]="art",["Pregunta"]="Contrasta foto y descripción",["__RequestVerificationToken"]=token }));
        Check(publication.StatusCode==HttpStatusCode.Redirect && (await admin.GetAsync(publication.Headers.Location)).IsSuccessStatusCode,"Análisis de publicación por formulario llega a resultado");
        var one = await service.ConsultarAsync("admin",new() { Tipo=TipoConsultaAdminGuard.Publicacion,ObjetivoId="art",Pregunta="Comparar fotografía y descripción" },default);
        Check(!fake.LastData.Contains("private@example.com") && !fake.LastData.Contains("SECRETTEST") && !fake.LastData.Contains("999888777"),"Redacción de contactos y secretos en texto");
        Check(!fake.LastData.Contains("PasswordHash") && !fake.LastData.Contains("SecurityStamp"),"No se envían entidades Identity");
        await service.ConsultarAsync("admin",new() { Tipo=TipoConsultaAdminGuard.Publicacion,ObjetivoId="twin",Pregunta="Buscar coincidencias exactas" },default);
        Check(fake.LastData.Contains("art"),"Coincidencia SHA256 con análisis previo");
        var report = await db.Reportes.FirstAsync();
        await service.ConsultarAsync("admin",new() { Tipo=TipoConsultaAdminGuard.Reporte,ObjetivoId=report.Id,Pregunta="Resumir hechos y antecedentes" },default);
        Check(fake.LastData.Contains("antecedentesAgregados"),"Resumen usa antecedentes agregados");
        Check(await db.SancionesUsuarios.CountAsync() == previousSanctions && article.Moderacion == EstadoModeracion.Visible,"IA no altera sanciones ni moderación");
        fake.Fail = true;
        try { await service.ConsultarAsync("admin",new() { Tipo=TipoConsultaAdminGuard.Estadisticas,Pregunta="Consultar estadísticas" },default); throw new Exception("No propagó fallo"); }
        catch(HttpRequestException) { Console.WriteLine("OK GUARD: Fallo del proveedor controlado y auditado"); }
        Check(await db.AnalisisAdminGuard.AnyAsync(a => !a.Completado),"Intento fallido conservado");
        fake.Fail = false;
        while(await db.AnalisisAdminGuard.CountAsync(a => a.AdministradorId == "admin") < 10)
            await service.ConsultarAsync("admin",new() { Tipo=TipoConsultaAdminGuard.Estadisticas,Pregunta="Consultar estadísticas" },default);
        try { await service.ConsultarAsync("admin",new() { Tipo=TipoConsultaAdminGuard.Estadisticas,Pregunta="Consultar estadísticas" },default); throw new Exception("No limitó uso"); }
        catch(InvalidOperationException ex) when(ex.Message.Contains("Límite")) { Console.WriteLine("OK GUARD: Límite horario persistido"); }
        Check(!AdminGuardImageReader.PublicAddress(IPAddress.Parse("127.0.0.1")) && !AdminGuardImageReader.PublicAddress(IPAddress.Parse("169.254.169.254")) && !AdminGuardImageReader.PublicAddress(IPAddress.Parse("::1")),"SSRF bloquea direcciones privadas y metadatos");
        var imageReader = new AdminGuardImageReader(new HttpClient(),Options.Create(new AdminGuardOptions()));
        try { await imageReader.LeerAsync("http://localhost/image.png",default); throw new Exception("URL insegura aceptada"); }
        catch(InvalidOperationException) { Console.WriteLine("OK GUARD: URL de imagen no autorizada rechazada"); }
        using var transport = new HttpClient(new Transport());
        var client = new AdminGuardClient(transport,Options.Create(new AdminGuardOptions { ApiKey="clave-simulada",Model="modelo-prueba" }));
        Check((await client.ConsultarAsync("Revisar","{}",[1,2,3],"image/png",default)) == "Respuesta de transporte simulada", "Contrato multimodal Groq verificado sin llamada externa");
    }
    sealed class Transport : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            var body = await request.Content!.ReadAsStringAsync(ct);
            using var json = JsonDocument.Parse(body);
            Check(request.RequestUri!.Host == "api.groq.com" && request.Headers.Authorization?.Scheme == "Bearer" && json.RootElement.GetProperty("messages")[1].GetProperty("content")[1].GetProperty("type").GetString() == "image_url","Petición multimodal y autenticación correctas");
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"Respuesta de transporte simulada\"}}]}",Encoding.UTF8,"application/json") };
        }
    }
}
