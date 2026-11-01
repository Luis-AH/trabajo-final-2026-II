using System.ComponentModel.DataAnnotations;

namespace SegundaOportunidad.Models;
public enum TipoSancion { Advertencia, SuspenderPublicaciones, SuspenderCuenta, ReactivarCuenta, ReactivarPublicaciones }
public sealed class SancionUsuario
{
    public long Id { get; set; }
    public string UsuarioId { get; set; } = "";
    public ApplicationUser Usuario { get; set; } = null!;
    public string AdministradorId { get; set; } = "";
    public ApplicationUser Administrador { get; set; } = null!;
    public TipoSancion Tipo { get; set; }
    [MaxLength(1000)] public string Motivo { get; set; } = "";
    public DateTime FechaUtc { get; set; }
    public DateTime? HastaUtc { get; set; }
}
