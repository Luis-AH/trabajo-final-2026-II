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
public sealed class AdminUsuariosController(ApplicationDbContext db, SancionUsuarioService sanciones, TimeProvider reloj) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? busqueda, bool? suspendidos, int pagina = 1, CancellationToken ct = default)
    {
        if (!ModelState.IsValid || busqueda?.Length > 150 || pagina is < 1 or > 100000) return BadRequest("Filtros inválidos.");
        var ahora = reloj.GetUtcNow().UtcDateTime;
        var query = db.Users.AsNoTracking();
        busqueda = busqueda?.Trim();
        if (!string.IsNullOrEmpty(busqueda)) query = query.Where(u => u.FullName.Contains(busqueda) || (u.Email != null && u.Email.Contains(busqueda)));
        if (suspendidos.HasValue) query = suspendidos.Value ? query.Where(u => u.AccountSuspendedUntilUtc > ahora) : query.Where(u => u.AccountSuspendedUntilUtc == null || u.AccountSuspendedUntilUtc <= ahora);
        var model = new AdminUsuariosViewModel { Busqueda = busqueda, Suspendidos = suspendidos, Total = await query.CountAsync(ct), AhoraUtc = ahora };
        model.Pagina = Math.Min(pagina, model.Paginas);
        model.Usuarios = await query.OrderBy(u => u.FullName).ThenBy(u => u.Id).Skip((model.Pagina - 1) * 20).Take(20)
            .Select(u => new AdminUsuarioFila { Usuario = u,
                Publicaciones = db.Articulos.IgnoreQueryFilters().Count(a => a.UserId == u.Id),
                Reportes = db.Reportes.Count(r => r.DenunciadoId == u.Id),
                Intercambios = db.Intercambios.IgnoreQueryFilters().Count(i => (i.ProposerId == u.Id || i.ReceiverId == u.Id) && i.Estado == EstadoIntercambio.Completado) }).ToListAsync(ct);
        return View(model);
    }
    [HttpGet]
    public async Task<IActionResult> Detalle(string id, int pagina = 1, CancellationToken ct = default)
    {
        if (pagina is < 1 or > 100000) return BadRequest();
        var usuario = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, ct);
        if (usuario == null) return NotFound();
        var historial = db.SancionesUsuarios.AsNoTracking().Where(s => s.UsuarioId == id);
        var paginas = Math.Max(1, ((await historial.CountAsync(ct)) + 19) / 20);
        pagina = Math.Min(pagina, paginas);
        return View(new AdminUsuarioDetalleViewModel { Usuario = usuario, AhoraUtc = reloj.GetUtcNow().UtcDateTime, Pagina = pagina, Paginas = paginas,
            Reportes = await db.Reportes.CountAsync(r => r.DenunciadoId == id, ct),
            Publicaciones = await db.Articulos.IgnoreQueryFilters().CountAsync(a => a.UserId == id, ct),
            Intercambios = await db.Intercambios.IgnoreQueryFilters().CountAsync(i => (i.ProposerId == id || i.ReceiverId == id) && i.Estado == EstadoIntercambio.Completado, ct),
            Historial = await historial.Include(s => s.Administrador).OrderByDescending(s => s.FechaUtc).ThenByDescending(s => s.Id).Skip((pagina - 1) * 20).Take(20).ToListAsync(ct) });
    }
    [HttpPost]
    public async Task<IActionResult> Sancionar(string id, [Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] TipoSancion tipo, string motivo, int? horas, string version, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest("Datos inválidos.");
        var admin = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (admin == null) return Forbid();
        try { await sanciones.AplicarAsync(id, admin, tipo, motivo, horas, version, ct); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (DbUpdateConcurrencyException) { return Conflict("El usuario cambió. Recarga su perfil antes de decidir."); }
        return RedirectToAction(nameof(Detalle), new { id });
    }
}
