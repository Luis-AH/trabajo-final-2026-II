using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Models;

namespace SegundaOportunidad.Controllers
{
    [Authorize(Roles = "Admin")]
    [AutoValidateAntiforgeryToken]
    public class SupervisorController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SegundaOportunidad.Services.RedisService _redisService;

        public SupervisorController(UserManager<ApplicationUser> userManager, SegundaOportunidad.Services.RedisService redisService)
        {
            _userManager = userManager;
            _redisService = redisService;
        }

        public async Task<IActionResult> Index()
        {
            // Autorización simple basada en email para el administrador/supervisor
            if (!User.IsInRole("Admin"))
            {
                return RedirectToAction("Dashboard", "Home");
            }

            // Traemos todos los usuarios que han registrado un DNI pero aún no están verificados
            var pendingUsers = await _userManager.Users
                .Where(u => u.Dni != null && u.Dni != "" && !u.IsVerified)
                .ToListAsync();

            return View(pendingUsers);
        }

        [HttpPost]
        public async Task<IActionResult> Approve(string id)
        {
            if (!User.IsInRole("Admin")) return Forbid();

            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                if (string.IsNullOrWhiteSpace(user.Dni) || !System.Text.RegularExpressions.Regex.IsMatch(user.Dni,"^[0-9]{8}$"))
                    return BadRequest("El usuario debe enviar una solicitud con DNI válido antes de aprobarlo.");
                user.IsVerified = true;
                user.VerifiedAtUtc = DateTime.UtcNow;
                user.VerifiedByAdminId = _userManager.GetUserId(User);
                if (!(await _userManager.UpdateAsync(user)).Succeeded) return Conflict("La cuenta cambió; recarga antes de decidir.");
                
                // Invalidar caché del dashboard para que el usuario vea el cambio
                await _redisService.RemoveCacheAsync($"dashboard_stats_{user.Id}");
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Reject(string id)
        {
            if (!User.IsInRole("Admin")) return Forbid();

            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                // Al rechazar, limpiamos el DNI para que pueda volver a enviarlo
                user.Dni = null; 
                user.IsVerified = false;
                user.VerifiedAtUtc = null;
                user.VerifiedByAdminId = null;
                await _userManager.UpdateAsync(user);
                
                await _redisService.RemoveCacheAsync($"dashboard_stats_{user.Id}");
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
