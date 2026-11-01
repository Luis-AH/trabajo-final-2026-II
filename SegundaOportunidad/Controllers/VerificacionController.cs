using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SegundaOportunidad.Models;
using SegundaOportunidad.Services;

namespace SegundaOportunidad.Controllers;
[Authorize,AutoValidateAntiforgeryToken]
public sealed class VerificacionController(UserManager<ApplicationUser> users,RedisService cache) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user=await users.GetUserAsync(User);
        return user==null ? Challenge() : View(user);
    }
    [HttpPost]
    public async Task<IActionResult> Solicitar([Required,RegularExpression("^[0-9]{8}$")] string dni,[Required,NombreCompleto] string nombreCompleto)
    {
        var user=await users.GetUserAsync(User);
        if(user==null) return Challenge();
        if(!ModelState.IsValid) { TempData["VerificationError"]="Introduce tus nombres y apellidos completos y un DNI de ocho dígitos."; return RedirectToAction(nameof(Index)); }
        if(User.IsInRole("Admin") || (user.IsVerified && user.VerifiedAtUtc!=null && user.VerifiedByAdminId!=null)) return RedirectToAction(nameof(Index));
        if(!string.IsNullOrEmpty(user.Dni)) { TempData["VerificationError"]="Tu solicitud ya está pendiente de revisión."; return RedirectToAction(nameof(Index)); }
        user.Dni=dni;
        user.FullName=NombreCompletoAttribute.Normalizar(nombreCompleto);
        user.IsVerified=false;
        user.VerifiedAtUtc=null;
        user.VerifiedByAdminId=null;
        var result=await users.UpdateAsync(user);
        if(!result.Succeeded) TempData["VerificationError"]="No se pudo guardar. Recarga y vuelve a intentarlo.";
        await cache.RemoveCacheAsync($"dashboard_stats_{user.Id}");
        return RedirectToAction(nameof(Index));
    }
}
