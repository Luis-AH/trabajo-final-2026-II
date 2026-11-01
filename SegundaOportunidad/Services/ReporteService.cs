using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;

namespace SegundaOportunidad.Services;
public sealed class ReporteService(ApplicationDbContext db, TimeProvider reloj)
{
    public async Task<string> CrearAsync(string actor, CrearReporteViewModel input, CancellationToken ct = default)
    {
        if ((string.IsNullOrEmpty(input.ArticuloId) == string.IsNullOrEmpty(input.UsuarioId)) || input.Motivo == null || !Enum.IsDefined(input.Motivo.Value)
            || string.IsNullOrWhiteSpace(input.Descripcion) || input.Descripcion.Trim().Length is < 10 or > 2000)
            throw new ArgumentException("Selecciona un objetivo, un motivo y una descripción de 10 a 2000 caracteres.");
        if (!string.IsNullOrWhiteSpace(input.EvidenciaUrl) && (input.EvidenciaUrl.Length > 1000 || !Uri.TryCreate(input.EvidenciaUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https"))
            throw new ArgumentException("La evidencia debe ser una URL HTTPS válida, de hasta 1000 caracteres.");
        string target;
        if (input.ArticuloId != null)
            target = await db.Articulos.Where(a => a.Id == input.ArticuloId).Select(a => a.UserId).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException();
        else target = await db.Users.Where(u => u.Id == input.UsuarioId).Select(u => u.Id).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException();
        if (target == actor) throw new ArgumentException("No puedes reportarte ni reportar tus propias publicaciones.");
        var key = actor + "|" + (input.ArticuloId != null ? "art:" + input.ArticuloId : "user:" + target);
        if (await db.Reportes.AnyAsync(r => r.ClaveAbierta == key, ct)) throw new ArgumentException("Ya tienes un reporte abierto sobre este objetivo.");
        var report = new Reporte { DenuncianteId = actor, DenunciadoId = target, ArticuloId = input.ArticuloId, Motivo = input.Motivo.Value,
            Descripcion = input.Descripcion.Trim(), EvidenciaUrl = input.EvidenciaUrl?.Trim(), CreadoUtc = reloj.GetUtcNow().UtcDateTime, ClaveAbierta = key };
        db.Reportes.Add(report);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.Sqlite.SqliteException { SqliteErrorCode: 19 })
        { throw new ArgumentException("No se pudo registrar: comprueba que no exista un reporte abierto y que los usuarios sigan disponibles."); }
        return report.Id;
    }
    public async Task DecidirAsync(string id, string admin, EstadoReporte estado, string justificacion, string version, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(estado) || string.IsNullOrWhiteSpace(justificacion) || justificacion.Trim().Length is < 10 or > 2000)
            throw new ArgumentException("Selecciona un estado y justifica la decisión con 10 a 2000 caracteres.");
        var report = await db.Reportes.SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw new KeyNotFoundException();
        if (report.Version != version) throw new DbUpdateConcurrencyException();
        if (!((report.Estado == EstadoReporte.Pendiente && estado is EstadoReporte.EnRevision or EstadoReporte.Descartado)
            || (report.Estado == EstadoReporte.EnRevision && estado is EstadoReporte.Resuelto or EstadoReporte.Descartado)))
            throw new ArgumentException("Transición no permitida. Los reportes cerrados conservan su decisión final.");
        db.DecisionesReportes.Add(new DecisionReporte { ReporteId = id, AdministradorId = admin, Anterior = report.Estado, Nuevo = estado, Justificacion = justificacion.Trim(), FechaUtc = reloj.GetUtcNow().UtcDateTime });
        report.Estado = estado;
        report.Version = Guid.NewGuid().ToString();
        if (estado is EstadoReporte.Resuelto or EstadoReporte.Descartado) report.ClaveAbierta = null;
        await db.SaveChangesAsync(ct);
    }
}
