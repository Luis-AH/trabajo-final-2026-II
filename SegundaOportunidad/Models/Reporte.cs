using System.ComponentModel.DataAnnotations;

namespace SegundaOportunidad.Models;
public enum MotivoReporte
{
    Spam,
    [Display(Name = "Publicidad engañosa")] PublicidadEnganosa,
    [Display(Name = "Posible estafa")] PosibleEstafa,
    [Display(Name = "Imagen sospechosa")] ImagenSospechosa,
    [Display(Name = "Contenido inapropiado")] ContenidoInapropiado,
    Otro
}
public enum EstadoReporte { Pendiente, EnRevision, Resuelto, Descartado }
public sealed class Reporte
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DenuncianteId { get; set; } = "";
    public ApplicationUser Denunciante { get; set; } = null!;
    public string DenunciadoId { get; set; } = "";
    public ApplicationUser Denunciado { get; set; } = null!;
    public string? ArticuloId { get; set; }
    public Articulo? Articulo { get; set; }
    public MotivoReporte Motivo { get; set; }
    [MaxLength(2000)] public string Descripcion { get; set; } = "";
    [MaxLength(1000)] public string? EvidenciaUrl { get; set; }
    public DateTime CreadoUtc { get; set; }
    public EstadoReporte Estado { get; set; }
    [ConcurrencyCheck] public string Version { get; set; } = Guid.NewGuid().ToString();
    public string? ClaveAbierta { get; set; }
    public List<DecisionReporte> Decisiones { get; set; } = [];
}
public sealed class DecisionReporte
{
    public long Id { get; set; }
    public string ReporteId { get; set; } = "";
    public Reporte Reporte { get; set; } = null!;
    public string AdministradorId { get; set; } = "";
    public ApplicationUser Administrador { get; set; } = null!;
    public EstadoReporte Anterior { get; set; }
    public EstadoReporte Nuevo { get; set; }
    [MaxLength(2000)] public string Justificacion { get; set; } = "";
    public DateTime FechaUtc { get; set; }
}
public sealed class CrearReporteViewModel
{
    public string? ArticuloId { get; set; }
    public string? UsuarioId { get; set; }
    [Required] public MotivoReporte? Motivo { get; set; }
    [Required, StringLength(2000, MinimumLength = 10)] public string Descripcion { get; set; } = "";
    [StringLength(1000)] public string? EvidenciaUrl { get; set; }
}
public sealed class BandejaReportesViewModel
{
    public EstadoReporte? Estado { get; set; }
    public MotivoReporte? Motivo { get; set; }
    public int Pagina { get; set; }
    public int Total { get; set; }
    public int Paginas => Math.Max(1, (Total + 19) / 20);
    public List<Reporte> Reportes { get; set; } = [];
}
public sealed class DetalleReporteViewModel
{
    public Reporte Reporte { get; set; } = null!;
    public List<DecisionReporte> Decisiones { get; set; } = [];
    public int Antecedentes { get; set; }
    public int Sanciones { get; set; }
}
