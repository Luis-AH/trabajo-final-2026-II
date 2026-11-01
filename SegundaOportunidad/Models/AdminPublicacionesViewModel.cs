namespace SegundaOportunidad.Models;

public sealed class AdminPublicacionesViewModel
{
    public string? Busqueda { get; set; }
    public string? ErrorBusqueda { get; set; }
    public EstadoModeracion? Estado { get; set; }
    public int? CategoriaId { get; set; }
    public int Pagina { get; set; }
    public int Total { get; set; }
    public int Paginas => Math.Max(1, (Total + 19) / 20);
    public List<Articulo> Publicaciones { get; set; } = [];
    public List<Categoria> Categorias { get; set; } = [];
}
