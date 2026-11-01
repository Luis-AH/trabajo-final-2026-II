using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;
using SegundaOportunidad.Services;

internal static class PublicationChecks
{
    public sealed class TestAlgolia : AlgoliaService
    {
        public TestAlgolia() : base(new ConfigurationBuilder().Build()) { }
        public override Task IndexArticuloAsync(Articulo article) => Task.CompletedTask;
    }
    public static readonly byte[] Png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
    static void Check(bool value,string message) { if(!value) throw new Exception(message); Console.WriteLine("OK FOTO: "+message); }
    public static async Task Run(HttpClient admin,IServiceProvider services)
    {
        using(var scope=services.CreateScope()) await CategoryCatalog.EnsureAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        var form=await admin.GetStringAsync("/Articulos/Create");
        Check(form.Contains("multipart/form-data") && form.Contains("name=\"imagen\""),"Formulario solicita archivo real");
        var options=Regex.Matches(form,"<option[^>]*>([^<]+)</option>").Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)).ToList();
        Check(options.Contains("Otros...") && options.IndexOf("Otros...")>options.IndexOf("Videojuegos y consolas"),"Categorías ampliadas con Otros al final");
        var token=WebUtility.HtmlDecode(Regex.Match(form,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        async Task<HttpResponseMessage> Publish(byte[]? photo,string fileName="foto.png",int category=1)
        {
            using var body=new MultipartFormDataContent();
            foreach(var pair in new Dictionary<string,string> { ["Title"]="Publicación con foto local",["Description"]="Objeto de prueba con fotografía adjunta",["CategoriaId"]=category.ToString(),["Modalidad"]="Intercambio",["EstadoFisico"]="BuenEstado",["__RequestVerificationToken"]=token }) body.Add(new StringContent(pair.Value),pair.Key);
            if(photo!=null) { var part=new ByteArrayContent(photo); part.Headers.ContentType=new MediaTypeHeaderValue("image/png"); body.Add(part,"imagen",fileName); }
            return await admin.PostAsync("/Articulos/Create",body);
        }
        using(var scope=services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var before=await db.Articulos.IgnoreQueryFilters().CountAsync();
            Check((await Publish(null)).IsSuccessStatusCode,"Publicación sin imagen devuelve formulario con error");
            Check((await Publish(System.Text.Encoding.UTF8.GetBytes("<svg>archivo incorrecto</svg>"))).IsSuccessStatusCode,"Archivo renombrado rechazado");
            Check((await Publish(Png,category:99999)).IsSuccessStatusCode,"Categoría inexistente rechazada");
            Check((await Publish(new byte[4*1024*1024+1])).IsSuccessStatusCode,"Foto mayor de 4 MB rechazada");
            Check(await db.Articulos.IgnoreQueryFilters().CountAsync()==before,"Entradas inválidas no insertan artículos");
        }
        Check((await Publish(Png)).StatusCode==HttpStatusCode.Redirect,"Publicación válida guarda artículo y foto");
        using var scope2=services.CreateScope();
        var context=scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article=await context.Articulos.SingleAsync(a => a.Title=="Publicación con foto local");
        var local=await context.FotosArticulos.SingleAsync(f => f.ArticuloId==article.Id);
        Check(local.Contenido.SequenceEqual(Png) && local.Mime=="image/png","Bytes originales persistidos");
        var image=await admin.GetAsync(article.ImageUrl);
        Check(image.IsSuccessStatusCode && image.Content.Headers.ContentType?.MediaType=="image/png","Imagen publicada servida como PNG");
        var reader=new AdminGuardImageReader(new HttpClient(),Options.Create(new AdminGuardOptions()),context);
        var fake=new GuardChecks.TestClient();
        var guard=new AdminGuardService(context,fake,reader,new LaterClock(),new AdminGuardGate());
        var analysisId=await guard.ConsultarAsync("admin",new() { Tipo=TipoConsultaAdminGuard.Publicacion,ObjetivoId=article.Id,Pregunta="Contrasta foto, título y descripción" },default);
        var audit=await context.AnalisisAdminGuard.SingleAsync(a => a.Id==analysisId);
        Check(audit.Completado && audit.ImagenSha256==Convert.ToHexString(SHA256.HashData(Png)).ToLowerInvariant(),"AdminGuard analiza exactamente los bytes publicados y registra SHA256");
        var review=await admin.GetStringAsync("/AdminPublicaciones/Detalle/"+article.Id);
        Check(review.Contains(article.ImageUrl!) && review.Contains("AdminGuard"),"Admin ve fotografía y acceso al análisis");
        article.Moderacion=EstadoModeracion.Oculta;
        await context.SaveChangesAsync();
        using var anonymous=new HttpClient(new HttpClientHandler { AllowAutoRedirect=false }) { BaseAddress=admin.BaseAddress };
        Check((await anonymous.GetAsync(article.ImageUrl)).StatusCode==HttpStatusCode.NotFound && (await admin.GetAsync(article.ImageUrl)).IsSuccessStatusCode,"Foto de publicación oculta solo accesible para Admin");
    }
    private sealed class LaterClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow.AddHours(2);
    }
}
