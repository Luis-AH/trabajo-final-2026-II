using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;
using SegundaOportunidad.Services;

namespace SegundaOportunidad.Controllers;
[Authorize, AutoValidateAntiforgeryToken]
public sealed class ReportesController(ApplicationDbContext db, ReporteService service) : Controller
{
    [HttpGet]
    public IActionResult Crear(string? articuloId, string? usuarioId) => View(new CrearReporteViewModel { ArticuloId = articuloId, UsuarioId = usuarioId });
    [HttpPost]
    public async Task<IActionResult> Crear(CrearReporteViewModel input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(input);
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actor == null) return Forbid();
        try { var id = await service.CrearAsync(actor, input, ct); return RedirectToAction(nameof(Detalle), new { id }); }
        catch (ArgumentException ex) { ModelState.AddModelError("", ex.Message); return View(input); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
    [HttpGet]
    public async Task<IActionResult> Detalle(string id, CancellationToken ct)
    {
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var report = await db.Reportes.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id && r.DenuncianteId == actor, ct);
        return report == null ? NotFound() : View(report);
    }
}
