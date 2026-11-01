using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;

namespace SegundaOportunidad.Services;

public sealed class AdminDashboardService(ApplicationDbContext db)
{
    public async Task<AdminDashboardViewModel> ObtenerAsync(DateTime desde, DateTime hasta, int? categoriaId, CancellationToken ct = default)
    {
        var fin = hasta.Date.AddDays(1);
        var ahora = DateTime.UtcNow;
        var articulos = db.Articulos.IgnoreQueryFilters().AsNoTracking().Where(a => a.CreatedAt >= desde.Date && a.CreatedAt < fin);
        var intercambios = db.Intercambios.IgnoreQueryFilters().AsNoTracking().Where(i => i.CreatedAt >= desde.Date && i.CreatedAt < fin);
        if (categoriaId.HasValue)
        {
            articulos = articulos.Where(a => a.CategoriaId == categoriaId);
            intercambios = intercambios.Where(i => i.RequestedArticle!.CategoriaId == categoriaId);
        }
        var estados = await intercambios.GroupBy(i => i.Estado)
            .Select(g => new { Estado = g.Key, Total = g.Count() }).ToListAsync(ct);
        var publicaciones = await articulos.GroupBy(a => a.IsActive)
            .Select(g => new { Activa = g.Key, Total = g.Count() }).ToListAsync(ct);
        var moderaciones = await articulos.GroupBy(a => a.Moderacion)
            .Select(g => new { Estado = g.Key, Total = g.Count() }).ToListAsync(ct);
        var actividad = await articulos.GroupBy(a => new { a.CreatedAt.Year, a.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Count() }).ToListAsync(ct);
        var evolucion = await intercambios.GroupBy(i => new { i.CreatedAt.Year, i.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Count() }).ToListAsync(ct);
        var categorias = await articulos.GroupBy(a => a.Categoria!.Name)
            .Select(g => new { Nombre = g.Key, Total = g.Count() })
            .OrderByDescending(g => g.Total).Take(10).ToListAsync(ct);
        var model = new AdminDashboardViewModel
        {
            Desde = desde.Date, Hasta = hasta.Date, CategoriaId = categoriaId,
            Usuarios = await db.Users.CountAsync(ct),
            ReportesAbiertos = await db.Reportes.CountAsync(r => r.Estado == EstadoReporte.Pendiente || r.Estado == EstadoReporte.EnRevision, ct),
            ReportesResueltos = await db.Reportes.CountAsync(r => r.Estado == EstadoReporte.Resuelto, ct),
            UsuariosSuspendidos = await db.Users.CountAsync(u => u.AccountSuspendedUntilUtc > ahora, ct),
            Ocultas = moderaciones.Where(x => x.Estado == EstadoModeracion.Oculta).Sum(x => x.Total),
            Suspendidas = moderaciones.Where(x => x.Estado == EstadoModeracion.Suspendida).Sum(x => x.Total),
            Eliminadas = moderaciones.Where(x => x.Estado == EstadoModeracion.Eliminada).Sum(x => x.Total),
            PublicacionesActivas = publicaciones.Where(x => x.Activa).Sum(x => x.Total),
            PublicacionesInactivas = publicaciones.Where(x => !x.Activa).Sum(x => x.Total),
            Pendientes = estados.Where(x => x.Estado == EstadoIntercambio.Pendiente).Sum(x => x.Total),
            Aceptados = estados.Where(x => x.Estado == EstadoIntercambio.Aceptado).Sum(x => x.Total),
            Completados = estados.Where(x => x.Estado == EstadoIntercambio.Completado).Sum(x => x.Total),
            Categorias = await db.Categorias.AsNoTracking().OrderBy(c => c.Name=="Otros...").ThenBy(c => c.Name).ToListAsync(ct),
            PorCategoria = categorias.Select(x => new DashboardSerie(x.Nombre, x.Total)).ToList()
        };
        for (var mes = new DateTime(desde.Year, desde.Month, 1); mes <= hasta; mes = mes.AddMonths(1))
        {
            model.Actividad.Add(new(mes.ToString("yyyy-MM"), actividad.Where(x => x.Year == mes.Year && x.Month == mes.Month).Sum(x => x.Total)));
            model.Evolucion.Add(new(mes.ToString("yyyy-MM"), evolucion.Where(x => x.Year == mes.Year && x.Month == mes.Month).Sum(x => x.Total)));
        }
        return model;
    }
}
