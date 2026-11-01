using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;

namespace SegundaOportunidad.Services;
public sealed class SancionUsuarioService(ApplicationDbContext db, TimeProvider reloj)
{
    public async Task AplicarAsync(string usuarioId, string adminId, TipoSancion tipo, string motivo, int? horas, string version, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(tipo) || string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length is < 5 or > 1000)
            throw new ArgumentException("Indica una acción válida y un motivo de 5 a 1000 caracteres.");
        var suspende = tipo is TipoSancion.SuspenderCuenta or TipoSancion.SuspenderPublicaciones;
        if (suspende && usuarioId == adminId) throw new ArgumentException("No puedes suspenderte a ti mismo.");
        if (suspende && (!horas.HasValue || horas.Value is < 1 or > 8760)) throw new ArgumentException("La suspensión debe durar de 1 a 8760 horas.");
        if (!suspende && horas.HasValue) throw new ArgumentException("Esta acción no admite duración.");
        var usuario = await db.Users.SingleOrDefaultAsync(u => u.Id == usuarioId, ct) ?? throw new KeyNotFoundException();
        if (usuario.SanctionVersion != version) throw new DbUpdateConcurrencyException();
        var ahora = reloj.GetUtcNow().UtcDateTime;
        DateTime? hasta = suspende ? ahora.AddHours(horas!.Value) : null;
        switch (tipo)
        {
            case TipoSancion.SuspenderCuenta:
                if (usuario.AccountSuspendedUntilUtc > ahora) throw new ArgumentException("La cuenta ya está suspendida; reactívala antes de aplicar otra suspensión.");
                usuario.AccountSuspendedUntilUtc = hasta; break;
            case TipoSancion.SuspenderPublicaciones:
                if (usuario.PublishingSuspendedUntilUtc > ahora) throw new ArgumentException("La publicación ya está suspendida para este usuario.");
                usuario.PublishingSuspendedUntilUtc = hasta; break;
            case TipoSancion.ReactivarCuenta:
                if (!(usuario.AccountSuspendedUntilUtc > ahora)) throw new ArgumentException("La cuenta no tiene una suspensión vigente.");
                usuario.AccountSuspendedUntilUtc = null; break;
            case TipoSancion.ReactivarPublicaciones:
                if (!(usuario.PublishingSuspendedUntilUtc > ahora)) throw new ArgumentException("No hay suspensión de publicaciones vigente.");
                usuario.PublishingSuspendedUntilUtc = null; break;
        }
        usuario.SanctionVersion = Guid.NewGuid().ToString();
        // Comparte el token de Identity para evitar sobrescribir cambios concurrentes del usuario.
        usuario.ConcurrencyStamp = Guid.NewGuid().ToString();
        db.SancionesUsuarios.Add(new SancionUsuario { UsuarioId = usuarioId, AdministradorId = adminId, Tipo = tipo, Motivo = motivo.Trim(), FechaUtc = ahora, HastaUtc = hasta });
        await db.SaveChangesAsync(ct);
    }
}
