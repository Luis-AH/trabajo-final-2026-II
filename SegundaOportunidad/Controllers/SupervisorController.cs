using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Models;

namespace SegundaOportunidad.Controllers
{
    [Authorize]
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
            if (User.Identity?.Name != "admin@segundaoportunidad.com")
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
            if (User.Identity?.Name != "admin@segundaoportunidad.com") return Forbid();

            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                user.IsVerified = true;
                await _userManager.UpdateAsync(user);
                
                // Invalidar caché del dashboard para que el usuario vea el cambio
                await _redisService.RemoveCacheAsync($"dashboard_stats_{user.Id}");
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Reject(string id)
        {
            if (User.Identity?.Name != "admin@segundaoportunidad.com") return Forbid();

            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                // Al rechazar, limpiamos el DNI para que pueda volver a enviarlo
                user.Dni = null; 
                await _userManager.UpdateAsync(user);
                
                await _redisService.RemoveCacheAsync($"dashboard_stats_{user.Id}");
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
