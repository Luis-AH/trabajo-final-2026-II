using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;

namespace SegundaOportunidad.Services;

public sealed class ChatSellerService(ApplicationDbContext db, TimeProvider clock)
{
    internal async Task<Dictionary<string,ChatSeller>> EvaluateAsync(IReadOnlyList<CatalogItem> items,CancellationToken ct)
    {
        var ids=items.Select(i=>i.SellerId).Distinct().Take(8).ToArray();
        // Nunca materializar ApplicationUser: no leer contraseñas, DNI, email, Rating por defecto ni reportes.
        var verified=await db.Users.AsNoTracking().Where(u=>ids.Contains(u.Id)).Select(u=>new {
            u.Id, Verified=u.IsVerified && u.VerifiedAtUtc!=null && u.VerifiedByAdminId!=null }).ToListAsync(ct);
        var proposed=await db.Intercambios.AsNoTracking().Where(i=>i.Estado==EstadoIntercambio.Completado && ids.Contains(i.ProposerId))
            .GroupBy(i=>i.ProposerId).Select(g=>new {Id=g.Key,Count=g.Count()}).ToDictionaryAsync(g=>g.Id,g=>g.Count,ct);
        var received=await db.Intercambios.AsNoTracking().Where(i=>i.Estado==EstadoIntercambio.Completado && ids.Contains(i.ReceiverId) && i.ReceiverId!=i.ProposerId)
            .GroupBy(i=>i.ReceiverId).Select(g=>new {Id=g.Key,Count=g.Count()}).ToDictionaryAsync(g=>g.Id,g=>g.Count,ct);
        var visible=await ChatCatalogAccess.Visible(db,clock.GetUtcNow().UtcDateTime).Where(a=>ids.Contains(a.UserId))
            .GroupBy(a=>a.UserId).Select(g=>new {Id=g.Key,Count=g.Count()}).ToDictionaryAsync(x=>x.Id,x=>x.Count,ct);
        var result=new Dictionary<string,ChatSeller>();
        for(var index=0;index<ids.Length;index++)
        {
            var id=ids[index];
            var total=proposed.GetValueOrDefault(id)+received.GetValueOrDefault(id);
            result[id]=new("Vendedor "+(index+1),verified.Any(u=>u.Id==id && u.Verified),total,visible.GetValueOrDefault(id),
                "Sin reseñas verificables registradas; no se utiliza la puntuación predeterminada.",
                "No hay datos suficientes para determinar la confiabilidad. La verificación y actividad son orientación, nunca garantía de seguridad ni ausencia de fraude.");
        }
        return result;
    }
}
