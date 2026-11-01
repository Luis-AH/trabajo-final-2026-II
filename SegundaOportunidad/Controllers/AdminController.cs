using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Data;
using SegundaOportunidad.Services;

namespace SegundaOportunidad.Controllers;

[Authorize(Roles = "Admin")]
public sealed class AdminController(AdminDashboardService dashboard, ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(DateTime? desde, DateTime? hasta, int? categoriaId, CancellationToken ct)
    {
        var hoy = DateTime.UtcNow.Date;
        var inicio = desde?.Date ?? new DateTime(hoy.Year, hoy.Month, 1).AddMonths(-11);
        var fin = hasta?.Date ?? hoy;
        if (!ModelState.IsValid || inicio > fin || fin > hoy || inicio < hoy.AddYears(-10))
            return BadRequest("Elige fechas válidas: desde hace diez años hasta hoy (UTC), con inicio anterior al fin.");
        if (categoriaId.HasValue && !await db.Categorias.AnyAsync(c => c.Id == categoriaId, ct))
            return BadRequest("La categoría no existe.");
        return View(await dashboard.ObtenerAsync(inicio, fin, categoriaId, ct));
    }
}
