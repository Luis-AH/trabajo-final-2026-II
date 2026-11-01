using SegundaOportunidad.Data;
using SegundaOportunidad.Models;
using Microsoft.EntityFrameworkCore;
namespace SegundaOportunidad.Services;
public static class ParticipationRules
{
    public static IQueryable<ApplicationUser> ApprovedParticipants(this ApplicationDbContext db) => db.Users.Where(u =>
        (u.IsVerified && u.VerifiedAtUtc!=null && u.VerifiedByAdminId!=null)
        || db.UserRoles.Any(ur => ur.UserId==u.Id && db.Roles.Any(r => r.Id==ur.RoleId && r.Name=="Admin")));
    public static async Task<bool> ChatAllowedAsync(this ApplicationDbContext db,string sender,string receiver,DateTime sentAt,CancellationToken ct=default)
    {
        var cutoff=await db.EstadosSistema.Where(s => s.Id=="chat-reset").Select(s => (DateTime?)s.FechaUtc).SingleOrDefaultAsync(ct);
        if(cutoff.HasValue && sentAt<=cutoff.Value) return false;
        var now=DateTime.UtcNow;
        return await db.ApprovedParticipants().CountAsync(u => (u.Id==sender || u.Id==receiver) && (u.AccountSuspendedUntilUtc==null || u.AccountSuspendedUntilUtc<=now),ct)==2;
    }
}
