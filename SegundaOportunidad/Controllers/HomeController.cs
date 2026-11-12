using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SegundaOportunidad.Models;
using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Services;

namespace SegundaOportunidad.Controllers;

public class HomeController : Controller
{
    private readonly SegundaOportunidad.Services.RedisService _redisService;
    private readonly Microsoft.AspNetCore.Identity.UserManager<SegundaOportunidad.Models.ApplicationUser> _userManager;
    private readonly SegundaOportunidad.Data.ApplicationDbContext _context;

    public HomeController(SegundaOportunidad.Services.RedisService redisService, Microsoft.AspNetCore.Identity.UserManager<SegundaOportunidad.Models.ApplicationUser> userManager, SegundaOportunidad.Data.ApplicationDbContext context)
    {
        _redisService = redisService;
        _userManager = userManager;
        _context = context;
    }

    public IActionResult Index()
    {
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            return RedirectToAction(nameof(Dashboard));
        }
        return View();
    }

    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> Dashboard()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var cacheKey = $"dashboard_stats_{user.Id}";
        var stats = await _redisService.GetCacheAsync<DashboardStats>(cacheKey);

        if (stats == null)
        {
            var articulos = _context.Articulos.Where(a => a.UserId == user.Id).ToList();
            
            stats = new DashboardStats
            {
                FullName = user.FullName ?? user.UserName ?? "Usuario",
                IsVerified = user.IsVerified,
                Dni = user.Dni,
                VentasActivas = articulos.Count(a => a.Modalidad == ModalidadArticulo.Venta && a.IsActive),
                IntercambiosRealizados = articulos.Count(a => a.Modalidad == ModalidadArticulo.Intercambio), // asumiendo todos por ahora
                DonacionesEntregadas = articulos.Count(a => a.Modalidad == ModalidadArticulo.Donacion),
                ArticulosSalvados = articulos.Count(),
                ArticulosPublicados = articulos.Count(),
                ConexionesLocales = user.FamiliesHelped // reusando esto para conexiones locales
            };
            // Cache for 1 hour
            await _redisService.SetCacheAsync(cacheKey, stats, TimeSpan.FromHours(1));
        }

        // Los contadores se leen siempre de SQLite: no conservar durante una hora cifras anteriores al seed o a una publicación.
        stats.ArticulosPublicados=await _context.Articulos.AsNoTracking().CountAsync(a=>a.UserId==user.Id);
        stats.PublicacionesDisponibles=await ChatCatalogAccess.Visible(_context,DateTime.UtcNow).CountAsync();
        stats.FullName=user.FullName??user.UserName??"Usuario";
        stats.IsVerified = User.IsInRole("Admin") || (user.IsVerified && user.VerifiedAtUtc != null && user.VerifiedByAdminId != null);
        stats.Dni = user.Dni;
        return View(stats);
    }

    [Microsoft.AspNetCore.Authorization.Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SubmitDni(string dni) => RedirectToAction("Index", "Verificacion");

    public class DashboardStats
    {
        public string FullName { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
        public string? Dni { get; set; }
        public int VentasActivas { get; set; }
        public int IntercambiosRealizados { get; set; }
        public int DonacionesEntregadas { get; set; }
        public int ArticulosSalvados { get; set; }
        public int ArticulosPublicados { get; set; }
        public int PublicacionesDisponibles { get; set; }
        public int ConexionesLocales { get; set; }
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
