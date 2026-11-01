using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;

namespace SegundaOportunidad.Services;

public sealed class ModeracionPublicacionService(ApplicationDbContext db)
{
    public async Task CambiarAsync(string id, string adminId, EstadoModeracion estado, string motivo, string version, CancellationToken ct)
    {
        if (!Enum.IsDefined(estado) || string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length is < 5 or > 1000)
            throw new ArgumentException("Indica un estado válido y un motivo de 5 a 1000 caracteres.");
        var articulo = await db.Articulos.IgnoreQueryFilters().SingleOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new KeyNotFoundException("Publicación no encontrada.");
        if (articulo.ModeracionVersion != version) throw new DbUpdateConcurrencyException();
        if (articulo.Moderacion == estado) throw new ArgumentException("La publicación ya tiene ese estado.");
        // Una eliminación lógica solo se revierte mediante restauración explícita.
        if (articulo.Moderacion == EstadoModeracion.Eliminada && estado != EstadoModeracion.Visible)
            throw new ArgumentException("Restaura la publicación antes de aplicar otra acción.");
        db.ModeracionesPublicaciones.Add(new ModeracionPublicacion {
            ArticuloId = id, AdministradorId = adminId, Anterior = articulo.Moderacion,
            Nuevo = estado, Motivo = motivo.Trim()
        });
        articulo.Moderacion = estado;
        articulo.ModeracionVersion = Guid.NewGuid().ToString();
        await db.SaveChangesAsync(ct);
    }
}
