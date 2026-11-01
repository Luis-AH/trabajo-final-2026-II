using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;
using SegundaOportunidad.Services;

namespace SegundaOportunidad.Controllers;
[Authorize(Roles="Admin"), AutoValidateAntiforgeryToken]
public sealed class SeguridadController(UserManager<ApplicationUser> users,SignInManager<ApplicationUser> signin,ApplicationDbContext db,IRecoveryEmailService email,ILogger<SeguridadController> logger) : Controller
{
    public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
    {
        Response.Headers.CacheControl="no-store";
        Response.Headers["Referrer-Policy"]="no-referrer";
        base.OnActionExecuting(context);
    }
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var user=await users.GetUserAsync(User);
        if(user==null) return Challenge();
        return View(new SeguridadViewModel { CorreoConfigurado=email.Configurado,RolesPropios=(await users.GetRolesAsync(user)).ToList(),
            Roles=await db.Roles.AsNoTracking().OrderBy(r => r.Name).Select(r => new RolSeguridadFila(r.Name ?? "",db.UserRoles.Count(ur => ur.RoleId==r.Id))).ToListAsync(ct) });
    }
    [HttpGet]
    public IActionResult Cambiar() => View(new CambiarClaveViewModel());
    [HttpPost]
    public async Task<IActionResult> Cambiar(CambiarClaveViewModel input)
    {
        if(!ModelState.IsValid) return View(input);
        var user=await users.GetUserAsync(User);
        if(user==null) return Challenge();
        var result=await users.ChangePasswordAsync(user,input.Actual,input.Nueva);
        if(!result.Succeeded)
        {
            ModelState.Clear();
            foreach(var error in result.Errors) ModelState.AddModelError("",error.Code=="PasswordMismatch" ? "La contraseña actual no es correcta." : error.Description);
            return View(new CambiarClaveViewModel());
        }
        await signin.RefreshSignInAsync(user);
        return RedirectToAction(nameof(Confirmacion),new { operacion="cambio" });
    }
    [AllowAnonymous,HttpGet]
    public IActionResult Recuperar() { ViewBag.CorreoConfigurado=email.Configurado; return View(new RecuperarClaveViewModel()); }
    [AllowAnonymous,HttpPost,EnableRateLimiting("recuperacion")]
    public async Task<IActionResult> Recuperar(RecuperarClaveViewModel input)
    {
        ViewBag.CorreoConfigurado=email.Configurado;
        if(!ModelState.IsValid) return View(input);
        if(email.Configurado)
        {
            var user=await users.FindByEmailAsync(input.Email);
            if(user!=null && await users.IsEmailConfirmedAsync(user))
            {
                var token=await users.GeneratePasswordResetTokenAsync(user);
                var code=WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                var url=QueryHelpers.AddQueryString(email.BaseUrl+"/Seguridad/Restablecer",new Dictionary<string,string?> { ["email"]=input.Email,["codigo"]=code });
                try { await email.EnviarAsync(input.Email,url); }
                catch(Exception ex) when(ex is System.Net.Mail.SmtpException or OperationCanceledException or FormatException)
                { logger.LogWarning("No se pudo entregar recuperación por correo ({Tipo}).",ex.GetType().Name); }
            }
        }
        return RedirectToAction(nameof(Confirmacion),new { operacion="solicitud" });
    }
    [AllowAnonymous,HttpGet]
    public IActionResult Restablecer(string? email,string? codigo)
    {
        if(string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(codigo) || codigo.Length>4096) return BadRequest("Enlace de recuperación no válido.");
        return View(new RestablecerClaveViewModel { Email=email,Codigo=codigo });
    }
    [AllowAnonymous,HttpPost,EnableRateLimiting("recuperacion")]
    public async Task<IActionResult> Restablecer(RestablecerClaveViewModel input)
    {
        if(!ModelState.IsValid) return View(input);
        string token;
        try { if(input.Codigo.Length>4096) throw new FormatException(); token=Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(input.Codigo)); }
        catch(FormatException) { ModelState.Clear(); ModelState.AddModelError("","Enlace inválido. Solicita uno nuevo."); return View(new RestablecerClaveViewModel { Email=input.Email }); }
        var user=await users.FindByEmailAsync(input.Email);
        var result=user!=null && await users.IsEmailConfirmedAsync(user) ? await users.ResetPasswordAsync(user,token,input.Nueva) : IdentityResult.Failed();
        if(!result.Succeeded)
        {
            ModelState.Clear(); ModelState.AddModelError("","No se pudo restablecer: verifica los requisitos de la nueva contraseña o solicita un enlace nuevo.");
            return View(new RestablecerClaveViewModel { Email=input.Email,Codigo=input.Codigo });
        }
        return RedirectToAction(nameof(Confirmacion),new { operacion="restablecer" });
    }
    [AllowAnonymous,HttpGet]
    public IActionResult Confirmacion(string? operacion) { ViewBag.Operacion=operacion; return View(); }
}
