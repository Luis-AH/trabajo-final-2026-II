using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;

namespace SegundaOportunidad.Services;

internal static class ChatCatalogAccess
{
    public static IQueryable<Articulo> Visible(ApplicationDbContext db, DateTime now) => db.Articulos.AsNoTracking()
        .Where(a=>a.IsActive && a.Moderacion==EstadoModeracion.Visible && a.User!=null &&
            (a.User.AccountSuspendedUntilUtc==null || a.User.AccountSuspendedUntilUtc<=now) &&
            (a.User.PublishingSuspendedUntilUtc==null || a.User.PublishingSuspendedUntilUtc<=now) &&
            ((a.User.IsVerified && a.User.VerifiedAtUtc!=null && a.User.VerifiedByAdminId!=null) ||
                db.UserRoles.Any(ur=>ur.UserId==a.UserId && db.Roles.Any(r=>r.Id==ur.RoleId && r.Name=="Admin"))))
        // Un intercambio completado retira ambos artículos de las recomendaciones,
        // aunque el controlador antiguo no haya desactivado IsActive.
        .Where(a=>!db.Intercambios.Any(i=>i.Estado==EstadoIntercambio.Completado && (i.OfferedArticleId==a.Id || i.RequestedArticleId==a.Id)));
}

public sealed class ChatCatalogService(ApplicationDbContext db, AlgoliaService algolia, IConfiguration config,
    TimeProvider clock, ILogger<ChatCatalogService> logger)
{
    internal async Task<CatalogResult> SearchAsync(string message, CancellationToken ct, bool useIndex=true)
    {
        var categories=await db.Categorias.AsNoTracking().Select(c=>new Categoria {Id=c.Id,Name=c.Name}).ToListAsync(ct);
        var request=ChatQueryParser.Parse(message,categories);
        if(!request.IsCatalog || request.Restricted) return new(request,[],"EF Core");
        var query=ChatCatalogAccess.Visible(db,clock.GetUtcNow().UtcDateTime);
        if(request.ArticleIds.Count>0) query=query.Where(a=>request.ArticleIds.Contains(a.Id));
        if(request.CategoryIds.Count>0) query=query.Where(a=>request.CategoryIds.Contains(a.CategoriaId));
        if(request.Condition!=null) query=query.Where(a=>a.EstadoFisico==request.Condition);
        if(request.Mode!=null) query=query.Where(a=>a.Modalidad==request.Mode);
        if(request.MinPrice!=null) query=query.Where(a=>a.Modalidad==ModalidadArticulo.Venta && a.Price!=null && a.Price>=request.MinPrice);
        if(request.MaxPrice!=null)
            query=request.ExclusiveMax ? query.Where(a=>a.Modalidad==ModalidadArticulo.Venta && a.Price!=null && a.Price<request.MaxPrice)
                : query.Where(a=>a.Modalidad==ModalidadArticulo.Venta && a.Price!=null && a.Price<=request.MaxPrice);
        if(request.Compare)
        {
            var productGroups=request.TermGroups.Where(g=>g[0] is "bicicleta" or "celular" or "laptop" or "libro" or "mochila" or "silla").ToList();
            if(productGroups.Count>1)
            {
                foreach(var group in productGroups) request.TermGroups.Remove(group);
                request.TermGroups.Add(productGroups.SelectMany(g=>g).Distinct().ToArray());
            }
        }
        foreach(var group in request.TermGroups)
        {
            var patterns=group.Select(term=>"%"+term.Replace("\\","\\\\").Replace("%","\\%").Replace("_","\\_")+"%").ToArray();
            query=query.Where(a=>patterns.Any(pattern=>EF.Functions.Like(
                (a.Title+" "+a.Description+" "+(a.Categoria==null ? "" : a.Categoria.Name)).ToLower()
                    .Replace("á","a").Replace("é","e").Replace("í","i").Replace("ó","o").Replace("ú","u").Replace("ü","u").Replace("ñ","n")
                    .Replace("Á","a").Replace("É","e").Replace("Í","i").Replace("Ó","o").Replace("Ú","u").Replace("Ü","u").Replace("Ñ","n"),pattern,"\\")));
        }
        var ids=new List<string>();
        var source="EF Core";
        // No usar credenciales de ejemplo del servicio compartido. Solo configuración explícita.
        if(useIndex && !string.IsNullOrWhiteSpace(config["Algolia:AppId"]) && !string.IsNullOrWhiteSpace(config["Algolia:WriteApiKey"]) && request.TermGroups.Count>0)
        {
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            try
            {
                ids=await algolia.SearchChatIdsAsync(string.Join(" ",request.TermGroups.Select(g=>g[0])),timeout.Token);
                source="Algolia + EF Core";
            }
            catch(OperationCanceledException) when(!ct.IsCancellationRequested) { logger.LogWarning("Búsqueda Algolia agotó 3 segundos; usando EF Core."); }
            catch(Exception ex) when(ex is not OperationCanceledException) { logger.LogWarning("Algolia no disponible ({Type}); usando EF Core.",ex.GetType().Name); }
        }
        // EF es la autoridad para filtros y disponibilidad. Algolia solo prioriza IDs;
        // el índice puede estar incompleto o contener artículos moderados.
        IOrderedQueryable<Articulo> ordered;
        if(request.Cheap || request.MaxPrice!=null || request.MinPrice!=null)
            ordered=query.OrderBy(a=>a.Price==null).ThenBy(a=>(double?)a.Price).ThenBy(a=>a.EstadoFisico).ThenBy(a=>a.Id);
        else
            ordered=query.OrderByDescending(a=>ids.Contains(a.Id)).ThenBy(a=>a.EstadoFisico).ThenByDescending(a=>a.CreatedAt).ThenBy(a=>a.Id);
        var items=await ordered.Take(8).Select(a=>new CatalogItem(a.Id,a.Title,a.Price,a.Modalidad,
            a.Categoria==null ? "Sin categoría" : a.Categoria.Name,a.EstadoFisico,
            a.Description.Length>600 ? a.Description.Substring(0,600) : a.Description,a.UserId)).ToListAsync(ct);
        return new(request,items.Select(a=>a with {Title=PublicText(a.Title),Description=PublicText(a.Description),Category=PublicText(a.Category)}).ToList(),source);
    }

    // Incluso los textos públicos pueden contener datos de contacto escritos por el anunciante.
    private static string PublicText(string text)
    {
        var safe=System.Text.RegularExpressions.Regex.Replace(text,@"[\w.+-]+@[\w.-]+\.[\p{L}]{2,}","[correo omitido]");
        safe=System.Text.RegularExpressions.Regex.Replace(safe,@"\b(?:DNI|tel[eé]fono|celular|WhatsApp)\s*[:=]?\s*\+?[\d ()-]{7,}","[dato de contacto omitido]",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return System.Text.RegularExpressions.Regex.Replace(safe,@"\bAIza[\w-]{20,}\b","[clave omitida]");
    }
}
