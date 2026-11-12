using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using SegundaOportunidad.Data;

namespace SegundaOportunidad.Services;
public sealed class VerifiedAccessFilter(ApplicationDbContext db) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var http=context.HttpContext;
        http.Items["CanParticipate"]=false;
        if(http.User.Identity?.IsAuthenticated!=true) return;
        if(http.User.IsInRole("Admin")) { http.Items["CanParticipate"]=true; return; }
        var id=http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var approved=await db.Users.AsNoTracking().AnyAsync(u => u.Id==id && u.IsVerified && u.VerifiedAtUtc!=null && u.VerifiedByAdminId!=null,http.RequestAborted);
        http.Items["CanParticipate"]=approved;
        if(approved) return;
        // Los filtros globales también alcanzan Razor Pages: Identity debe permitir
        // cerrar sesión y gestionar credenciales aunque la cuenta aún no esté aprobada.
        if(context.ActionDescriptor is Microsoft.AspNetCore.Mvc.RazorPages.PageActionDescriptor
            && context.ActionDescriptor.RouteValues.TryGetValue("area",out var area)
            && string.Equals(area,"Identity",StringComparison.OrdinalIgnoreCase)) return;
        var controller=context.RouteData.Values["controller"]?.ToString();
        var action=context.RouteData.Values["action"]?.ToString();
        var read=HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method);
        // Excepciones de cuenta y verificación; ninguna operación de negocio.
        var allowed=controller is "Verificacion" or "Seguridad"
            || (read && controller=="Ml")
            || (read && controller=="Articulos" && action is "Index" or "Details" or "Imagen" or "Explorar")
            || (read && controller=="Home" && action is "Index" or "Dashboard" or "Privacy" or "Error")
            || (read && controller=="Perfil" && action=="Index");
        if(allowed) return;
        if(read) context.Result=new RedirectToActionResult("Index","Verificacion",null);
        else context.Result=new ObjectResult("Debes solicitar tu verificación y recibir aprobación del administrador antes de realizar esta acción.") { StatusCode=403 };
    }
}
