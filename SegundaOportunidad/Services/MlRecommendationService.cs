using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;

namespace SegundaOportunidad.Services;

public sealed record MlArticle(string Id,string Titulo,string Descripcion,string Categoria,string Modalidad,string Estado,decimal? Precio,string? Imagen,string Enlace,double Similitud,string Motivo);
public sealed record MlAnswer(string Algoritmo,string Version,string Criterio,IReadOnlyList<MlArticle> Publicaciones,bool Disponible);

public sealed class MlRecommendationService(ApplicationDbContext db,ScikitContentModel model,TimeProvider clock)
{
    public async Task<MlAnswer> RecommendAsync(string? userId,string? query,string? articleId,int take,CancellationToken ct)
    {
        if(!model.Ready)return new("TF-IDF Scikit-learn","","Modelo no disponible. Entrena o publica el artefacto antes de usar recomendaciones.",[],false);
        var version=model.Version;
        var visible=ChatCatalogAccess.Visible(db,clock.GetUtcNow().UtcDateTime);
        var items=await visible.OrderByDescending(a=>a.CreatedAt).ThenBy(a=>a.Id).Take(2000)
            .Select(a=>new {a.Id,a.Title,a.Description,Category=a.Categoria==null?"":a.Categoria.Name,a.Modalidad,a.EstadoFisico,a.Price,a.ImageUrl,a.UserId}).ToListAsync(ct);
        var vectors=items.ToDictionary(a=>a.Id,a=>model.Transform(string.Join(' ',a.Title,a.Title,a.Title,a.Category,a.Category,a.Description,a.Modalidad,a.EstadoFisico)));
        Dictionary<int,double> target;
        string criterion;
        if(!string.IsNullOrWhiteSpace(articleId))
        {
            if(!vectors.TryGetValue(articleId,out target!))return new(model.Algorithm,model.Version,"La publicación base no está disponible.",[],true);
            criterion="Artículos similares por contenido";
        }
        else if(!string.IsNullOrWhiteSpace(query)) {target=model.Transform(query);criterion="Afinidad con tu búsqueda";}
        else
        {
            var interestIds=userId==null ? [] : await db.Intercambios.AsNoTracking().Where(i=>i.ProposerId==userId||i.ReceiverId==userId)
                .OrderByDescending(i=>i.CreatedAt).Take(20).Select(i=>i.ProposerId==userId?i.RequestedArticleId:i.OfferedArticleId).ToListAsync(ct);
            var interests=items.Where(a=>interestIds.Contains(a.Id)||a.UserId==userId).Select(a=>(IReadOnlyDictionary<int,double>)vectors[a.Id]).ToList();
            target=ScikitContentModel.Centroid(interests.Count>0 ? interests : vectors.Values.Select(v=>(IReadOnlyDictionary<int,double>)v));
            criterion=interests.Count>0 ? "Contenido relacionado con tus publicaciones e intercambios; señal orientativa" : "Contenido representativo del catálogo; no se presume popularidad";
        }
        var scores=items.Where(a=>a.Id!=articleId && (userId==null||a.UserId!=userId)).Select(a=>new {Article=a,Score=ScikitContentModel.Similarity(target,vectors[a.Id])})
            .Where(x=>x.Score>0.000001).OrderByDescending(x=>x.Score).ThenBy(x=>x.Article.Id).Take(Math.Clamp(take,1,12)).ToList();
        // EF vuelve a validar autorización justo antes de entregar el resultado.
        var ids=scores.Select(x=>x.Article.Id).ToArray();
        var allowed=(await ChatCatalogAccess.Visible(db,clock.GetUtcNow().UtcDateTime).Where(a=>ids.Contains(a.Id)).Select(a=>a.Id).ToListAsync(ct)).ToHashSet();
        if(model.Version!=version)return new(model.Algorithm,model.Version,"El modelo se actualizó durante la consulta. Vuelve a intentarlo.",[],false);
        return new(model.Algorithm,version,criterion,scores.Where(x=>allowed.Contains(x.Article.Id)).Select(x=>new MlArticle(x.Article.Id,x.Article.Title,
            x.Article.Description.Length>220?x.Article.Description[..220]:x.Article.Description,x.Article.Category,x.Article.Modalidad.ToString(),x.Article.EstadoFisico.ToString(),
            x.Article.Modalidad==ModalidadArticulo.Venta?x.Article.Price:null,x.Article.ImageUrl,"/Articulos/Details/"+Uri.EscapeDataString(x.Article.Id),Math.Round(x.Score,6),criterion+". Similitud del texto; no es una valoración ni garantía de calidad.")).ToList(),true);
    }
}
