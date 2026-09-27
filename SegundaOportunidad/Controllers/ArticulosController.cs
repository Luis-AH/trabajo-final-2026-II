using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;
using SegundaOportunidad.Services;

namespace SegundaOportunidad.Controllers
{
    [Authorize]
    public class ArticulosController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AlgoliaService _algoliaService;

        public ArticulosController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, AlgoliaService algoliaService)
        {
            _context = context;
            _userManager = userManager;
            _algoliaService = algoliaService;
        }

        // GET: Articulos
        [AllowAnonymous]
        public async Task<IActionResult> Index(string searchString, int? categoriaId)
        {
            var query = _context.Articulos.Include(a => a.Categoria).Include(a => a.User).AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                // Búsqueda en Algolia
                var ids = await _algoliaService.SearchArticulosIdsAsync(searchString);
                query = query.Where(a => ids.Contains(a.Id));
            }

            if (categoriaId.HasValue)
            {
                query = query.Where(a => a.CategoriaId == categoriaId.Value);
            }

            ViewData["Categorias"] = new SelectList(_context.Categorias, "Id", "Name");
            return View(await query.ToListAsync());
        }

        // GET: Articulos/Details/5
        [AllowAnonymous]
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var articulo = await _context.Articulos
                .Include(a => a.Categoria)
                .Include(a => a.User)
                .FirstOrDefaultAsync(m => m.Id == id);
                
            if (articulo == null)
            {
                return NotFound();
            }

            return View(articulo);
        }

        // GET: Articulos/Create
        public IActionResult Create()
        {
            ViewData["CategoriaId"] = new SelectList(_context.Categorias, "Id", "Name");
            return View();
        }

        // POST: Articulos/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,Description,ImageUrl,Modalidad,EstadoFisico,Price,ExchangeDetails,CategoriaId")] Articulo articulo)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            ModelState.Remove("UserId");
            ModelState.Remove("User");
            ModelState.Remove("Categoria");

            if (ModelState.IsValid)
            {
                articulo.Id = Guid.NewGuid().ToString();
                articulo.UserId = user.Id;
                articulo.CreatedAt = DateTime.UtcNow;
                articulo.IsActive = true;

                _context.Add(articulo);
                await _context.SaveChangesAsync();

                // Indexar en Algolia
                try
                {
                    await _algoliaService.IndexArticuloAsync(articulo);
                }
                catch (Exception ex)
                {
                    // Si Algolia falla, no detenemos el flujo principal pero idealmente se registraría en logs
                    Console.WriteLine($"Error indexando en Algolia: {ex.Message}");
                }

                return RedirectToAction(nameof(Index));
            }
            ViewData["CategoriaId"] = new SelectList(_context.Categorias, "Id", "Name", articulo.CategoriaId);
            return View(articulo);
        }
    }
}
