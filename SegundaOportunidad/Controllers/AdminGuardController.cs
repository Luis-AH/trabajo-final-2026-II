using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;
using SegundaOportunidad.Services;

namespace SegundaOportunidad.Controllers;
[Authorize(Roles = "Admin"), AutoValidateAntiforgeryToken]
public sealed class AdminGuardController(ApplicationDbContext db, AdminGuardService service) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(TipoConsultaAdminGuard? tipo, string? objetivoId, CancellationToken ct) =>
        View(await Panel(new ConsultaAdminGuardViewModel { Tipo = tipo, ObjetivoId = objetivoId },null,ct));
    private async Task<PanelAdminGuardViewModel> Panel(ConsultaAdminGuardViewModel input, string? error, CancellationToken ct) => new() {
        Configurado = service.Configurado, Consulta = input, Error = error,
        Historial = await db.AnalisisAdminGuard.AsNoTracking().Where(a => a.AdministradorId == User.FindFirstValue(ClaimTypes.NameIdentifier)).OrderByDescending(a => a.CreadoUtc).Take(20).ToListAsync(ct)
    };
    [HttpPost]
    public async Task<IActionResult> Consultar(ConsultaAdminGuardViewModel input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("Index", await Panel(input,"Revisa el tipo, el objetivo y la pregunta.",ct));
        var admin = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (admin == null) return Forbid();
        try { var id = await service.ConsultarAsync(admin,input,ct); return RedirectToAction(nameof(Resultado),new { id }); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException or System.Text.Json.JsonException or OperationCanceledException)
        {
            var mensaje = ex is ArgumentException or InvalidOperationException ? ex.Message : "No se pudo consultar Groq. Comprueba conexión, credenciales, modelo y cuotas; no se aplicó ninguna sanción.";
            return View("Index",await Panel(input,mensaje,CancellationToken.None));
        }
    }
    [HttpGet]
    public async Task<IActionResult> Resultado(string id, CancellationToken ct)
    {
        var admin = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var audit = await db.AnalisisAdminGuard.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id && a.AdministradorId == admin,ct);
        return audit == null ? NotFound() : View(audit);
    }
}
