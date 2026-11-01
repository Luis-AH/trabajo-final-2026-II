namespace SegundaOportunidad.Models;
public sealed class AdminUsuariosViewModel
{
    public string? Busqueda { get; set; }
    public bool? Suspendidos { get; set; }
    public int Pagina { get; set; }
    public int Total { get; set; }
    public int Paginas => Math.Max(1, (Total + 19) / 20);
    public DateTime AhoraUtc { get; set; }
    public List<AdminUsuarioFila> Usuarios { get; set; } = [];
}
public sealed class AdminUsuarioFila
{
    public ApplicationUser Usuario { get; set; } = null!;
    public int Publicaciones { get; set; }
    public int Intercambios { get; set; }
    public int Reportes { get; set; }
}
public sealed class AdminUsuarioDetalleViewModel
{
    public int Reportes { get; set; }
    public ApplicationUser Usuario { get; set; } = null!;
    public int Publicaciones { get; set; }
    public int Intercambios { get; set; }
    public int Pagina { get; set; }
    public int Paginas { get; set; }
    public DateTime AhoraUtc { get; set; }
    public List<SancionUsuario> Historial { get; set; } = [];
}
