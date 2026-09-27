using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;

namespace SegundaOportunidad.Controllers
{
    [Authorize]
    public class PerfilController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PerfilController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Mi Perfil
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var articulos = await _context.Articulos
                .Include(a => a.Categoria)
                .Where(a => a.UserId == user.Id)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            ViewData["User"] = user;
            ViewData["Articulos"] = articulos;
            return View(user);
        }

        // Mis Publicaciones
        public async Task<IActionResult> MisPublicaciones()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var articulos = await _context.Articulos
                .Include(a => a.Categoria)
                .Where(a => a.UserId == user.Id)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return View(articulos);
        }

        // Donaciones
        public async Task<IActionResult> Donaciones()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var articulos = await _context.Articulos
                .Include(a => a.Categoria)
                .Include(a => a.User)
                .Where(a => a.Modalidad == ModalidadArticulo.Donacion && a.IsActive)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            var misdonaciones = await _context.Articulos
                .Include(a => a.Categoria)
                .Where(a => a.UserId == user.Id && a.Modalidad == ModalidadArticulo.Donacion)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            ViewData["MisDonaciones"] = misdonaciones;
            return View(articulos);
        }

        // Intercambios
        public async Task<IActionResult> Intercambios()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var articulos = await _context.Articulos
                .Include(a => a.Categoria)
                .Include(a => a.User)
                .Where(a => a.Modalidad == ModalidadArticulo.Intercambio && a.IsActive)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            var misintercambios = await _context.Articulos
                .Include(a => a.Categoria)
                .Where(a => a.UserId == user.Id && a.Modalidad == ModalidadArticulo.Intercambio)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            ViewData["MisIntercambios"] = misintercambios;
            return View(articulos);
        }
    }
}
