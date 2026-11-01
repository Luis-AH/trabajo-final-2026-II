using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;
using SegundaOportunidad.Services;

internal static class ReportChecks
{
    static void Check(bool condition, string label) { if (!condition) throw new Exception(label); Console.WriteLine("OK REPORTES: " + label); }
    public static async Task Run(HttpClient admin, HttpClient member, IServiceProvider services)
    {
        async Task<string> Token(HttpClient client, string path)
        {
            var response = await client.GetAsync(path);
            Check(response.IsSuccessStatusCode, "Formulario " + path);
            return WebUtility.HtmlDecode(Regex.Match(await response.Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        }
        Check((await member.GetAsync("/AdminReportes")).StatusCode == HttpStatusCode.Redirect, "Usuario común no accede a bandeja");
        var token = await Token(member, "/Reportes/Crear?usuarioId=admin");
        var sent = await member.PostAsync("/Reportes/Crear", new FormUrlEncodedContent(new Dictionary<string,string> { ["UsuarioId"]="admin", ["Motivo"]="Spam", ["Descripcion"]="Evidencia de comportamiento repetitivo", ["__RequestVerificationToken"]=token }));
        Check(sent.StatusCode == HttpStatusCode.Redirect, "Reporte de usuario creado mediante HTTP");
        var location = sent.Headers.Location!.ToString();
        Check((await member.GetAsync(location)).StatusCode == HttpStatusCode.OK && (await admin.GetAsync(location)).StatusCode == HttpStatusCode.NotFound, "Detalle privado para el denunciante");
        string reportId;
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var svc = scope.ServiceProvider.GetRequiredService<ReporteService>();
            reportId = (await db.Reportes.SingleAsync()).Id;
            async Task Reject(Func<Task> action, string label)
            {
                try { await action(); throw new Exception("Aceptó operación inválida: " + label); }
                catch (Exception ex) when (ex is ArgumentException or DbUpdateConcurrencyException) { Console.WriteLine("OK REPORTES: Rechazo " + label); }
            }
            await Reject(async () => { await svc.CrearAsync("member", new() { UsuarioId="admin", Motivo=MotivoReporte.Spam, Descripcion="Otro reporte sobre el mismo usuario" }); }, "duplicado abierto");
            await Reject(async () => { await svc.CrearAsync("member", new() { ArticuloId="art", Motivo=MotivoReporte.Otro, Descripcion="No se permite reportar objeto propio" }); }, "autorreporte de publicación");
            await Reject(async () => { await svc.CrearAsync("admin", new() { UsuarioId="member", Motivo=MotivoReporte.Otro, Descripcion="URL no segura como evidencia", EvidenciaUrl="javascript:alert(1)" }); }, "URL de evidencia insegura");
            var report = await db.Reportes.SingleAsync();
            await Reject(() => svc.DecidirAsync(reportId,"admin",EstadoReporte.Resuelto,"Resolución sin revisión previa",report.Version), "resolución directa sin revisión");
            var old = report.Version;
            await svc.DecidirAsync(reportId,"admin",EstadoReporte.EnRevision,"Se inicia revisión de evidencias",report.Version);
            await Reject(() => svc.DecidirAsync(reportId,"admin",EstadoReporte.Resuelto,"Resolución con versión antigua",old), "conflicto de versión");
        }
        var adminToken = await Token(admin, "/AdminReportes/Detalle/" + reportId);
        string version;
        using (var scope = services.CreateScope()) version = (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Reportes.SingleAsync()).Version;
        var decided = await admin.PostAsync("/AdminReportes/Decidir", new FormUrlEncodedContent(new Dictionary<string,string> { ["id"]=reportId, ["estado"]="Resuelto", ["justificacion"]="Evidencias revisadas y decisión justificada", ["version"]=version, ["__RequestVerificationToken"]=adminToken }));
        Check(decided.StatusCode == HttpStatusCode.Redirect, "Resolución con cookie y antiforgery reales");
        Check((await admin.GetAsync("/AdminReportes")).StatusCode == HttpStatusCode.OK, "Bandeja renderizada");
        Check((await admin.PostAsync("/AdminReportes/Decidir", new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode == HttpStatusCode.BadRequest, "Decisión sin antiforgery rechazada");
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var svc = scope.ServiceProvider.GetRequiredService<ReporteService>();
            Check(await db.DecisionesReportes.CountAsync() == 2 && (await db.Reportes.SingleAsync()).ClaveAbierta == null, "Historial y cierre persistidos");
            var closed = await db.Reportes.SingleAsync();
            try { await svc.DecidirAsync(closed.Id,"admin",EstadoReporte.EnRevision,"Intento de reabrir reporte cerrado",closed.Version); throw new Exception("Reabrió reporte cerrado"); }
            catch (ArgumentException) { Console.WriteLine("OK REPORTES: Reporte cerrado no se reabre"); }
            Check(await db.SancionesUsuarios.CountAsync() == 2, "Reportar y resolver no aplican sanciones automáticamente");
            var articleReport = await svc.CrearAsync("admin", new() { ArticuloId="art", Motivo=MotivoReporte.ImagenSospechosa, Descripcion="Revisar imagen y descripción del artículo" });
            Check((await db.Reportes.SingleAsync(r => r.Id == articleReport)).DenunciadoId == "member", "Propietario de publicación determinado por servidor");
            var article = await db.Reportes.SingleAsync(r => r.Id == articleReport);
            await svc.DecidirAsync(articleReport,"admin",EstadoReporte.Descartado,"No hay evidencia suficiente para continuar",article.Version);
            Check(article.Estado == EstadoReporte.Descartado && article.ClaveAbierta == null, "Descarte justificado cierra reporte");
            await svc.CrearAsync("member", new() { UsuarioId="admin", Motivo=MotivoReporte.Otro, Descripcion="Nuevo hecho después del cierre anterior" });
            Check(await db.Reportes.CountAsync() == 3, "Nuevo reporte permitido tras cierre");
        }
    }
}
