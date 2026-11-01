using System.ComponentModel.DataAnnotations;

namespace SegundaOportunidad.Models;

public sealed class ModeracionPublicacion
{
    public long Id { get; set; }
    public string ArticuloId { get; set; } = "";
    public Articulo Articulo { get; set; } = null!;
    public string AdministradorId { get; set; } = "";
    public ApplicationUser Administrador { get; set; } = null!;
    public EstadoModeracion Anterior { get; set; }
    public EstadoModeracion Nuevo { get; set; }
    [MaxLength(1000)] public string Motivo { get; set; } = "";
    public DateTime FechaUtc { get; set; } = DateTime.UtcNow;
}
