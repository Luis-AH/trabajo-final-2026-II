using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Data;

namespace SegundaOportunidad.Services;
public sealed class SuspensionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db, TimeProvider reloj)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var id = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var estado = await db.Users.AsNoTracking().Where(u => u.Id == id)
                .Select(u => new { u.AccountSuspendedUntilUtc, u.PublishingSuspendedUntilUtc }).SingleOrDefaultAsync(context.RequestAborted);
            var ahora = reloj.GetUtcNow().UtcDateTime;
            var logout = context.Request.Path.Equals("/Identity/Account/Logout", StringComparison.OrdinalIgnoreCase);
            var crear = context.Request.Path.StartsWithSegments("/Articulos/Create", StringComparison.OrdinalIgnoreCase);
            if (!logout && (estado == null || estado.AccountSuspendedUntilUtc > ahora || (crear && estado.PublishingSuspendedUntilUtc > ahora)))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "text/plain; charset=utf-8";
                await context.Response.WriteAsync("Acceso restringido por una suspensión vigente. Puedes cerrar sesión. Las suspensiones temporales vencen automáticamente.");
                return;
            }
        }
        await next(context);
    }
}
