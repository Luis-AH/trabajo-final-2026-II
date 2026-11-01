using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;

namespace SegundaOportunidad.Services;
public sealed class AdminGuardGate { public SemaphoreSlim Semaphore { get; } = new(1,1); }
public sealed class AdminGuardService(ApplicationDbContext db, IAdminGuardClient client, IAdminGuardImageReader images, TimeProvider reloj, AdminGuardGate gate)
{
    public bool Configurado => client.Configurado;
    public static string Sanear(string? value, int max = 2000)
    {
        var s = (value ?? "")[..Math.Min(value?.Length ?? 0, max)];
        s = Regex.Replace(s, @"[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}", "[correo omitido]");
        s = Regex.Replace(s, @"(?i)(bearer\s+\S+|(?:password|contraseña|token|api[_-]?key|secret)\s*[:=]\s*\S+)", "[secreto omitido]");
        s = Regex.Replace(s, @"\b\d[\d\s()+-]{6,}\d\b", "[número omitido]");
        return Regex.Replace(s, @"\b[A-Za-z0-9_-]{24,}\b", "[identificador omitido]");
    }
    public async Task<string> ConsultarAsync(string admin, ConsultaAdminGuardViewModel input, CancellationToken ct)
    {
        if (!client.Configurado) throw new InvalidOperationException("Configura AdminGuard:ApiKey y AdminGuard:Model para habilitar consultas reales.");
        if (input.Tipo == null || !Enum.IsDefined(input.Tipo.Value) || string.IsNullOrWhiteSpace(input.Pregunta) || input.Pregunta.Trim().Length is < 5 or > 500)
            throw new ArgumentException("Selecciona un tipo y escribe una pregunta de 5 a 500 caracteres.");
        if (input.Tipo != TipoConsultaAdminGuard.Estadisticas && string.IsNullOrWhiteSpace(input.ObjetivoId)) throw new ArgumentException("Selecciona una publicación o reporte.");
        if (!await gate.Semaphore.WaitAsync(0, ct)) throw new InvalidOperationException("Hay otro análisis en curso. Inténtalo al terminar.");
        AnalisisAdminGuard? audit = null;
        try
        {
            var now = reloj.GetUtcNow().UtcDateTime;
            if (await db.AnalisisAdminGuard.CountAsync(a => a.AdministradorId == admin && a.CreadoUtc > now.AddHours(-1), ct) >= 10)
                throw new InvalidOperationException("Límite alcanzado: diez consultas por administrador en una hora, incluidos intentos fallidos.");
            var pregunta = Sanear(input.Pregunta,500);
            audit = new AnalisisAdminGuard { AdministradorId = admin, Tipo = input.Tipo.Value, Consulta = pregunta, Modelo = client.Modelo, CreadoUtc = now };
            db.AnalisisAdminGuard.Add(audit);
            await db.SaveChangesAsync(ct);
            object datos;
            byte[]? image = null; string? mime = null;
            if (input.Tipo == TipoConsultaAdminGuard.Publicacion)
            {
                var article = await db.Articulos.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(a => a.Id == input.ObjetivoId, ct) ?? throw new KeyNotFoundException("Publicación no encontrada.");
                audit.ArticuloId = article.Id;
                if (string.IsNullOrWhiteSpace(article.ImageUrl)) throw new InvalidOperationException("La publicación no tiene una fotografía para analizar.");
                (image, mime) = await images.LeerAsync(article.ImageUrl,ct);
                audit.ImagenSha256 = Convert.ToHexString(SHA256.HashData(image)).ToLowerInvariant();
                var duplicates = await db.AnalisisAdminGuard.AsNoTracking().Where(a => a.Completado && a.ImagenSha256 == audit.ImagenSha256 && a.ArticuloId != article.Id)
                    .Select(a => a.ArticuloId).Distinct().Take(10).ToListAsync(ct);
                var reports = await db.Reportes.CountAsync(r => r.ArticuloId == article.Id,ct);
                datos = new { titulo = Sanear(article.Title,300), descripcion = Sanear(article.Description), estado = article.Moderacion.ToString(), reportesRecibidos = reports,
                    sha256 = audit.ImagenSha256, coincidenciasHistoricasExactas = duplicates, alcance = "Coincidencias solo con imágenes analizadas previamente; sus publicaciones pueden haber cambiado. No implica fraude." };
            }
            else if (input.Tipo == TipoConsultaAdminGuard.Reporte)
            {
                var report = await db.Reportes.AsNoTracking().SingleOrDefaultAsync(r => r.Id == input.ObjetivoId,ct) ?? throw new KeyNotFoundException("Reporte no encontrado.");
                audit.ReporteId = report.Id;
                var reasons = await db.Reportes.Where(r => r.DenunciadoId == report.DenunciadoId).GroupBy(r => new { r.Motivo, r.Estado })
                    .Select(g => new { motivo = g.Key.Motivo.ToString(), estado = g.Key.Estado.ToString(), total = g.Count() }).ToListAsync(ct);
                var sanctions = await db.SancionesUsuarios.Where(s => s.UsuarioId == report.DenunciadoId).GroupBy(s => s.Tipo)
                    .Select(g => new { tipo = g.Key.ToString(), total = g.Count() }).ToListAsync(ct);
                datos = new { motivo = report.Motivo.ToString(), estado = report.Estado.ToString(), descripcionRedactada = Sanear(report.Descripcion), antecedentesAgregados = reasons, decisionesSancionesAgregadas = sanctions,
                    nota = "Los reportes son alegaciones. Los totales de sanciones incluyen advertencias y reactivaciones; no determinan culpabilidad." };
            }
            else
            {
                datos = new { fechaCorteUtc = now, usuarios = await db.Users.CountAsync(ct), publicaciones = await db.Articulos.IgnoreQueryFilters().CountAsync(ct),
                    intercambios = await db.Intercambios.GroupBy(i => i.Estado).Select(g => new { estado = g.Key.ToString(), total = g.Count() }).ToListAsync(ct),
                    reportes = await db.Reportes.GroupBy(r => new { r.Motivo, r.Estado }).Select(g => new { motivo = g.Key.Motivo.ToString(), estado = g.Key.Estado.ToString(), total = g.Count() }).ToListAsync(ct),
                    criterioPrioridad = "Sugiere prioridades por tipo y cantidad de reportes abiertos; no hay certeza de infracción ni datos suficientes para medir gravedad real." };
            }
            var respuesta = await client.ConsultarAsync(pregunta,JsonSerializer.Serialize(datos),image,mime,ct);
            if (string.IsNullOrWhiteSpace(respuesta) || respuesta.Length > 8000) throw new InvalidOperationException("Respuesta vacía o demasiado larga.");
            audit.Resultado = Sanear(respuesta,8000);
            audit.Completado = true;
            await db.SaveChangesAsync(ct);
            return audit.Id;
        }
        catch (Exception ex)
        {
            if (audit != null)
            {
                audit.Resultado = ex switch { KeyNotFoundException => "Objetivo no encontrado.", InvalidOperationException => "No se pudo completar: configuración, imagen o respuesta inválida.", OperationCanceledException => "Consulta cancelada o tiempo agotado.", _ => "Proveedor no disponible o respuesta no válida." };
                await db.SaveChangesAsync(CancellationToken.None);
            }
            throw;
        }
        finally { gate.Semaphore.Release(); }
    }
}
