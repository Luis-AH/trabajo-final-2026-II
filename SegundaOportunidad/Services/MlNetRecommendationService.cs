using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;

namespace SegundaOportunidad.Services;

public sealed class MlNetRecommendationService(ApplicationDbContext db, MlNetModels model, TimeProvider clock)
{
    public async Task<MlAnswer> RecommendAsync(string query, int count, CancellationToken ct)
    {
        var items = await ChatCatalogAccess.Visible(db, clock.GetUtcNow().UtcDateTime)
            .OrderByDescending(a => a.CreatedAt).ThenBy(a => a.Id).Take(2000)
            .Select(a => new { a.Id, a.Title, a.Description, Category = a.Categoria == null ? "" : a.Categoria.Name,
                a.Price, a.Modalidad, a.EstadoFisico, a.ImageUrl }).ToListAsync(ct);
        if (items.Count == 0)
            return new("ML.NET · FeaturizeText + coseno", "1", "Sin publicaciones disponibles", [], true);
        var texts = items.Select(a => string.Join(' ', a.Title, a.Title, a.Title,
            a.Category, a.Category, a.Description, a.Modalidad, a.EstadoFisico));
        var vectors = model.Vectorize(new[] { query }.Concat(texts));
        var ranked = items.Select((item, index) => new { Item = item, Score = MlNetModels.Similarity(vectors[0], vectors[index + 1]) })
            .Where(result => result.Score > 0.000001).OrderByDescending(result => result.Score)
            .ThenBy(result => result.Item.Id).Take(Math.Clamp(count, 1, 12)).ToList();
        var ids = ranked.Select(result => result.Item.Id).ToArray();
        var allowed = (await ChatCatalogAccess.Visible(db, clock.GetUtcNow().UtcDateTime)
            .Where(a => ids.Contains(a.Id)).Select(a => a.Id).ToListAsync(ct)).ToHashSet();
        var articles = ranked.Where(result => allowed.Contains(result.Item.Id)).Select(result => {
            var a = result.Item;
            return new MlArticle(a.Id, a.Title, a.Description.Length > 220 ? a.Description[..220] : a.Description,
                a.Category, a.Modalidad.ToString(), a.EstadoFisico.ToString(),
                a.Modalidad == ModalidadArticulo.Venta ? a.Price : null, a.ImageUrl,
                "/Articulos/Details/" + Uri.EscapeDataString(a.Id), Math.Round(result.Score, 6),
                "Afinidad del texto calculada con ML.NET; no garantiza calidad ni seguridad.");
        }).ToList();
        return new("ML.NET · FeaturizeText + coseno", "1", "Afinidad con tu búsqueda", articles, true);
    }
}
