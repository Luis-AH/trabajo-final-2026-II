namespace SegundaOportunidad.Models;
// Una fotografía por artículo; se separa del catálogo para no cargar el BLOB en listados.
public sealed class FotoArticulo
{
    public string ArticuloId { get; set; } = "";
    public Articulo Articulo { get; set; } = null!;
    public byte[] Contenido { get; set; } = [];
    public string Mime { get; set; } = "";
}
