using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;

internal static class GuardWorkspaceChecks
{
    static void Check(bool condition,string label) { if(!condition) throw new Exception(label); Console.WriteLine("OK ESPACIO IA: "+label); }
    public static async Task Run(HttpClient admin,HttpClient member,IServiceProvider services)
    {
        using(var scope=services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var original=await db.Articulos.SingleAsync(a => a.Id=="art");
            original.ImageUrl="https://images.unsplash.com/test";
            for(var i=0;i<1001;i++) db.Articulos.Add(new Articulo { Id="gallery-"+i,Title=i==1000 ? "Hallazgo lejano único" : "Artículo de galería "+i,
                Description="Publicación temporal para probar búsqueda y paginación",UserId="member",CategoriaId=1,CreatedAt=DateTime.UtcNow.AddDays(-10).AddMinutes(-i),Moderacion=i%2==0 ? EstadoModeracion.Oculta : EstadoModeracion.Visible });
            await db.SaveChangesAsync();
        }
        var home=await admin.GetStringAsync("/AdminGuard");
        Check(Regex.Matches(home,"class=\"guard-action-card\"").Count==3 && !home.Contains("id=\"ObjetivoId\""),"Inicio ofrece tres tareas sin selector masivo");
        var token=WebUtility.HtmlDecode(Regex.Match(home,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        var data=await admin.GetStringAsync("/AdminGuard/Datos");
        Check(data.Contains("name=\"Tipo\" value=\"Estadisticas\""),"Consultas de datos en página independiente");
        var gallery=await admin.GetStringAsync("/AdminGuard/Publicaciones");
        var second=await admin.GetStringAsync("/AdminGuard/Publicaciones?pagina=2");
        Check(Regex.Matches(gallery,"<article class=\"guard-publication-card\"").Count==12 && !gallery.Contains("<table"),"Publicaciones en doce tarjetas, sin tabla");
        var ids1=Regex.Matches(gallery,"href=\"/AdminGuard/Analizar/([^\"]+)\"").Select(m => m.Groups[1].Value).ToHashSet();
        var ids2=Regex.Matches(second,"href=\"/AdminGuard/Analizar/([^\"]+)\"").Select(m => m.Groups[1].Value).ToHashSet();
        Check(ids1.Count==12 && ids2.Count==12 && !ids1.Overlaps(ids2),"Páginas sin publicaciones repetidas");
        var search=await admin.GetStringAsync("/AdminGuard/Publicaciones?busqueda="+Uri.EscapeDataString("hallazgo lejano"));
        Check(search.Contains("gallery-1000") && Regex.Matches(search,"<article class=\"guard-publication-card\"").Count==1,"Búsqueda encuentra publicación fuera de las primeras mil");
        var wildcard=await admin.GetStringAsync("/AdminGuard/Publicaciones?busqueda=%25");
        Check(!wildcard.Contains("<article class=\"guard-publication-card\""),"Comodines escritos se buscan como texto literal");
        var mismatch=await admin.GetStringAsync("/AdminGuard/Publicaciones?busqueda="+Uri.EscapeDataString("Hallazgo lejano")+"&estado=Visible");
        Check(!mismatch.Contains("<article class=\"guard-publication-card\""),"Filtro de nombre combinado con estado");
        Check((await admin.GetAsync("/AdminGuard/Publicaciones?pagina=0")).StatusCode==HttpStatusCode.BadRequest,"Paginación inválida rechazada");
        var empty=await admin.GetStringAsync("/AdminGuard/Publicaciones?busqueda=no-existe-nada-aqui");
        Check(empty.Contains("guard-empty"),"Estado vacío con opción de nueva búsqueda");
        foreach(var path in new[] {"/AdminGuard/Datos","/AdminGuard/Publicaciones","/AdminGuard/Analizar/art","/AdminGuard/Reportes","/AdminGuard/Resumen/no-existe"})
            Check((await member.GetAsync(path)).StatusCode==HttpStatusCode.Redirect,"Solo Admin accede a "+path);
        string reportId;
        using(var scope=services.CreateScope()) reportId=await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Reportes.Select(r => r.Id).FirstAsync();
        Check((await admin.GetAsync("/AdminGuard/Reportes")).IsSuccessStatusCode && (await admin.GetAsync("/AdminGuard/Resumen/"+reportId)).IsSuccessStatusCode,"Reportes y antecedentes tienen páginas propias");
        async Task<string> Consult(TipoConsultaAdminGuard type,string? target)
        {
            var fields=new Dictionary<string,string> { ["Tipo"]=type.ToString(),["Pregunta"]="Revisa los datos y explica sus límites",["__RequestVerificationToken"]=token };
            if(target!=null) fields["ObjetivoId"]=target;
            var response=await admin.PostAsync("/AdminGuard/Consultar",new FormUrlEncodedContent(fields));
            Check(response.StatusCode==HttpStatusCode.Redirect,"Consulta registrada: "+type);
            Check((await admin.GetAsync(response.Headers.Location)).IsSuccessStatusCode,"Resultado accesible: "+type);
            return response.Headers.Location!.ToString();
        }
        await Consult(TipoConsultaAdminGuard.Estadisticas,null);
        await Consult(TipoConsultaAdminGuard.Publicacion,"art");
        await Consult(TipoConsultaAdminGuard.Reporte,reportId);
        using(var scope=services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Check(await db.AnalisisAdminGuard.CountAsync(a => a.Completado && a.AdministradorId=="admin")==3,"Se guardan resultados de las tres tareas");
        }
        var analyze=await admin.GetStringAsync("/AdminGuard/Analizar/art");
        Check(analyze.Contains("guard-history-card") && analyze.Contains("value=\"art\""),"Historial por publicación y objetivo fijo en formulario");
    }
}
