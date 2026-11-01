using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;
using SegundaOportunidad.Services;

namespace SegundaOportunidad.Controllers;
[Authorize(Roles = "Admin"), AutoValidateAntiforgeryToken]
public sealed class AdminReportesController(ApplicationDbContext db, ReporteService service) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(EstadoReporte? estado, MotivoReporte? motivo, string? usuarioId, int pagina = 1, CancellationToken ct = default)
    {
        if (!ModelState.IsValid || pagina is < 1 or > 100000 || (estado.HasValue && !Enum.IsDefined(estado.Value)) || (motivo.HasValue && !Enum.IsDefined(motivo.Value))) return BadRequest("Filtros inválidos.");
        var query = db.Reportes.AsNoTracking();
        if (estado.HasValue) query = query.Where(r => r.Estado == estado);
        if (motivo.HasValue) query = query.Where(r => r.Motivo == motivo);
        if (!string.IsNullOrEmpty(usuarioId)) query = query.Where(r => r.DenunciadoId == usuarioId);
        ViewBag.UsuarioId = usuarioId;
        var model = new BandejaReportesViewModel { Estado = estado, Motivo = motivo, Total = await query.CountAsync(ct) };
        model.Pagina = Math.Min(pagina, model.Paginas);
        model.Reportes = await query.Include(r => r.Denunciado).OrderByDescending(r => r.CreadoUtc).ThenBy(r => r.Id).Skip((model.Pagina - 1) * 20).Take(20).ToListAsync(ct);
        return View(model);
    }
    [HttpGet]
    public async Task<IActionResult> Detalle(string id, CancellationToken ct)
    {
        var report = await db.Reportes.IgnoreQueryFilters().AsNoTracking().Include(r => r.Denunciante).Include(r => r.Denunciado).Include(r => r.Articulo).SingleOrDefaultAsync(r => r.Id == id, ct);
        if (report == null) return NotFound();
        return View(new DetalleReporteViewModel { Reporte = report,
            Decisiones = await db.DecisionesReportes.AsNoTracking().Where(d => d.ReporteId == id).Include(d => d.Administrador).OrderBy(d => d.FechaUtc).ToListAsync(ct),
            Antecedentes = await db.Reportes.CountAsync(r => r.DenunciadoId == report.DenunciadoId && r.Id != id, ct),
            Sanciones = await db.SancionesUsuarios.CountAsync(s => s.UsuarioId == report.DenunciadoId, ct) });
    }
    [HttpPost]
    public async Task<IActionResult> Decidir(string id, [Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] EstadoReporte estado, string justificacion, string version, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest("Indica estado, justificación y versión.");
        var admin = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (admin == null) return Forbid();
        try { await service.DecidirAsync(id, admin, estado, justificacion, version, ct); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (DbUpdateConcurrencyException) { return Conflict("El reporte cambió. Recarga antes de decidir."); }
        return RedirectToAction(nameof(Detalle), new { id });
    }
}
