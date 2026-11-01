namespace SegundaOportunidad.Models;

public sealed class AdminDashboardViewModel
{
    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }
    public int? CategoriaId { get; set; }
    public int Usuarios { get; set; }
    public int UsuariosSuspendidos { get; set; }
    public int ReportesAbiertos { get; set; }
    public int ReportesResueltos { get; set; }
    public int PublicacionesActivas { get; set; }
    public int PublicacionesInactivas { get; set; }
    public int Ocultas { get; set; }
    public int Suspendidas { get; set; }
    public int Eliminadas { get; set; }
    public int Pendientes { get; set; }
    public int Aceptados { get; set; }
    public int Completados { get; set; }
    public List<Categoria> Categorias { get; set; } = [];
    public List<DashboardSerie> Actividad { get; set; } = [];
    public List<DashboardSerie> Evolucion { get; set; } = [];
    public List<DashboardSerie> PorCategoria { get; set; } = [];
}

public sealed record DashboardSerie(string Etiqueta, int Total);
