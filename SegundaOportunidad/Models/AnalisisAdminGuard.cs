using System.ComponentModel.DataAnnotations;

namespace SegundaOportunidad.Models;
public enum TipoConsultaAdminGuard { Publicacion, Reporte, Estadisticas }
public sealed class AnalisisAdminGuard
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string AdministradorId { get; set; } = "";
    public ApplicationUser Administrador { get; set; } = null!;
    public string? ArticuloId { get; set; }
    public string? ReporteId { get; set; }
    public TipoConsultaAdminGuard Tipo { get; set; }
    public DateTime CreadoUtc { get; set; }
    [MaxLength(500)] public string Consulta { get; set; } = "";
    [MaxLength(150)] public string Modelo { get; set; } = "";
    [MaxLength(64)] public string? ImagenSha256 { get; set; }
    [MaxLength(8000)] public string Resultado { get; set; } = "";
    public bool Completado { get; set; }
}
public sealed class ConsultaAdminGuardViewModel
{
    [Required] public TipoConsultaAdminGuard? Tipo { get; set; }
    [StringLength(100)] public string? ObjetivoId { get; set; }
    [Required, StringLength(500, MinimumLength = 5)] public string Pregunta { get; set; } = "Revisa las señales que requieren verificación y sugiere próximos pasos.";
}
public sealed class PanelAdminGuardViewModel
{
    public bool Configurado { get; set; }
    public string? Error { get; set; }
    public ConsultaAdminGuardViewModel Consulta { get; set; } = new();
    public List<AnalisisAdminGuard> Historial { get; set; } = [];
    public Articulo? Publicacion { get; set; }
    public Reporte? ReporteActual { get; set; }
    public int Antecedentes { get; set; }
    public int DecisionesSanciones { get; set; }
}
public sealed class GuardGalleryViewModel
{
    public string? Busqueda { get; set; }
    public EstadoModeracion? Estado { get; set; }
    public int Pagina { get; set; }
    public int Total { get; set; }
    public int Paginas => Math.Max(1,(Total+11)/12);
    public List<GuardPublicationCard> Publicaciones { get; set; } = [];
}
public sealed record GuardPublicationCard(string Id,string Titulo,string Descripcion,string? Imagen,string Categoria,EstadoModeracion Estado,DateTime CreadoUtc);
public sealed class GuardReportsViewModel
{
    public string? Busqueda { get; set; }
    public EstadoReporte? Estado { get; set; }
    public int Pagina { get; set; }
    public int Total { get; set; }
    public int Paginas => Math.Max(1,(Total+11)/12);
    public List<Reporte> Reportes { get; set; } = [];
}
