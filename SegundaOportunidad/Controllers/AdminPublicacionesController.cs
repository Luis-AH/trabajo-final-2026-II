using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;
using SegundaOportunidad.Services;

namespace SegundaOportunidad.Controllers;

[Authorize(Roles = "Admin")]
[AutoValidateAntiforgeryToken]
public sealed class AdminPublicacionesController(ApplicationDbContext db, AlgoliaService algolia, ModeracionPublicacionService moderacion, ILogger<AdminPublicacionesController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? busqueda, EstadoModeracion? estado, int? categoriaId, int pagina = 1, CancellationToken ct = default)
    {
        if (!ModelState.IsValid || pagina < 1 || pagina > 100000 || busqueda?.Length > 150 || (estado.HasValue && !Enum.IsDefined(estado.Value)))
            return BadRequest("Filtros inválidos.");
        if (categoriaId.HasValue && !await db.Categorias.AnyAsync(c => c.Id == categoriaId, ct)) return BadRequest("Categoría no encontrada.");
        var model = new AdminPublicacionesViewModel { Busqueda = busqueda?.Trim(), Estado = estado, CategoriaId = categoriaId, Pagina = pagina };
        var query = db.Articulos.IgnoreQueryFilters().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(model.Busqueda))
        {
            try {
                var ids = await algolia.SearchAllArticulosIdsAsync(model.Busqueda, ct);
                query = query.Where(a => ids.Contains(a.Id));
            } catch (Exception ex) when (ex is not OperationCanceledException) {
                logger.LogWarning(ex, "Búsqueda administrativa Algolia no disponible");
                model.ErrorBusqueda = "Algolia no está disponible. Se muestran coincidencias locales en título y descripción.";
                query = query.Where(a => a.Title.Contains(model.Busqueda!) || a.Description.Contains(model.Busqueda!));
            }
        }
        if (estado.HasValue) query = query.Where(a => a.Moderacion == estado);
        if (categoriaId.HasValue) query = query.Where(a => a.CategoriaId == categoriaId);
        model.Total = await query.CountAsync(ct);
        model.Pagina = Math.Min(pagina, model.Paginas);
        model.Publicaciones = await query.Include(a => a.User).Include(a => a.Categoria)
            .OrderByDescending(a => a.CreatedAt).ThenBy(a => a.Id).Skip((model.Pagina - 1) * 20).Take(20).ToListAsync(ct);
        model.Categorias = await db.Categorias.AsNoTracking().OrderBy(c => c.Name=="Otros...").ThenBy(c => c.Name).ToListAsync(ct);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Detalle(string id, CancellationToken ct)
    {
        var articulo = await db.Articulos.IgnoreQueryFilters().AsNoTracking().Include(a => a.User).Include(a => a.Categoria).SingleOrDefaultAsync(a => a.Id == id, ct);
        if (articulo == null) return NotFound();
        ViewBag.Historial = await db.ModeracionesPublicaciones.AsNoTracking().Include(m => m.Administrador)
            .Where(m => m.ArticuloId == id).OrderByDescending(m => m.FechaUtc).Take(100).ToListAsync(ct);
        return View(articulo);
    }

    [HttpPost]
    public async Task<IActionResult> Cambiar(string id, [Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] EstadoModeracion estado, string motivo, string version, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest("Datos inválidos.");
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (adminId == null) return Forbid();
        try { await moderacion.CambiarAsync(id, adminId, estado, motivo, version, ct); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (DbUpdateConcurrencyException) { return Conflict("La publicación cambió. Recarga y revisa su estado antes de continuar."); }
        return RedirectToAction(nameof(Detalle), new { id });
    }
}
