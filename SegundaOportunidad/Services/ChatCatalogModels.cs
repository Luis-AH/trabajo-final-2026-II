using SegundaOportunidad.Models;

namespace SegundaOportunidad.Services;

// Contratos de lectura: nunca serializar entidades de Identity en el chatbot.
public sealed record ChatSeller(string Referencia, bool IdentidadVerificada, int IntercambiosCompletados,
    int PublicacionesVisibles, string Valoraciones, string Orientacion);
public sealed record ChatProduct(string Id, string Titulo, decimal? Precio, string Modalidad,
    string Categoria, string Estado, string Descripcion, string Enlace, ChatSeller Vendedor, IReadOnlyList<string> Motivos);
public sealed record ChatAnswer(string Respuesta, IReadOnlyList<ChatProduct> Publicaciones,
    string Fuente, bool GeminiUtilizado)
{
    public string Orquestador => "Microsoft Semantic Kernel";
}
internal sealed record CatalogItem(string Id, string Title, decimal? Price, ModalidadArticulo Mode,
    string Category, EstadoFisico Condition, string Description, string SellerId);
internal sealed record CatalogResult(ChatCatalogQuery Query, IReadOnlyList<CatalogItem> Items, string Source);

public sealed class ChatCatalogQuery
{
    public List<string[]> TermGroups { get; } = [];
    public List<string> ArticleIds { get; } = [];
    public List<int> CategoryIds { get; } = [];
    public decimal? MaxPrice { get; set; }
    public decimal? MinPrice { get; set; }
    public bool ExclusiveMax { get; set; }
    public bool Cheap { get; set; }
    public bool Recommend { get; set; }
    public bool Compare { get; set; }
    public bool Sellers { get; set; }
    public bool IsCatalog { get; set; }
    public bool Restricted { get; set; }
    public ModalidadArticulo? Mode { get; set; }
    public EstadoFisico? Condition { get; set; }
}
