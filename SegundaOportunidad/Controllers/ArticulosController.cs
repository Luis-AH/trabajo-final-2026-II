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
                Console.WriteLine($"[Algolia Search] Query: {searchString}, Found {ids.Count} hits. IDs: {string.Join(", ", ids)}");
                query = query.Where(a => ids.Contains(a.Id));
            }

            if (categoriaId.HasValue)
            {
                query = query.Where(a => a.CategoriaId == categoriaId.Value);
            }

            ViewData["Categorias"] = new SelectList(_context.Categorias.OrderBy(c => c.Name=="Otros...").ThenBy(c => c.Name), "Id", "Name");
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

            var userId = _userManager.GetUserId(User);
            if (!string.IsNullOrEmpty(userId) && userId != articulo.UserId)
            {
                ViewData["ArticulosParaIntercambio"] = await _context.Articulos
                    .Where(a => a.UserId == userId && a.IsActive)
                    .OrderBy(a => a.Title)
                    .ToListAsync();
            }

            return View(articulo);
        }

        // GET: Articulos/Create
        public IActionResult Create()
        {
            ViewData["CategoriaId"] = new SelectList(_context.Categorias.OrderBy(c => c.Name=="Otros...").ThenBy(c => c.Name), "Id", "Name");
            return View();
        }

        // POST: Articulos/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(5*1024*1024)]
        public async Task<IActionResult> Create([Bind("Title,Description,Modalidad,EstadoFisico,Price,ExchangeDetails,CategoriaId")] Articulo articulo,IFormFile? imagen,CancellationToken ct)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            ModelState.Remove("UserId");
            ModelState.Remove("User");
            ModelState.Remove("Categoria");
            FotoArticulo? foto=null;
            try { foto=await ArticuloPhotoService.LeerAsync(imagen,ct); }
            catch(ArgumentException ex) { ModelState.AddModelError("imagen",ex.Message); }
            if(!await _context.Categorias.AnyAsync(c => c.Id==articulo.CategoriaId,ct)) ModelState.AddModelError("CategoriaId","Selecciona una categoría válida.");

            if (ModelState.IsValid)
            {
                articulo.Id = Guid.NewGuid().ToString();
                articulo.UserId = user.Id;
                articulo.CreatedAt = DateTime.UtcNow;
                articulo.IsActive = true;
                articulo.ImageUrl = "/Articulos/Imagen/"+articulo.Id;
                foto!.ArticuloId=articulo.Id;

                _context.Add(articulo);
                _context.FotosArticulos.Add(foto);
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
            ViewData["CategoriaId"] = new SelectList(_context.Categorias.OrderBy(c => c.Name=="Otros...").ThenBy(c => c.Name), "Id", "Name", articulo.CategoriaId);
            return View(articulo);
        }

        [AllowAnonymous,HttpGet]
        public async Task<IActionResult> Imagen(string id,CancellationToken ct)
        {
            var articles=User.IsInRole("Admin") ? _context.Articulos.IgnoreQueryFilters() : _context.Articulos;
            if(!await articles.AnyAsync(a => a.Id==id,ct)) return NotFound();
            var foto=await _context.FotosArticulos.AsNoTracking().SingleOrDefaultAsync(f => f.ArticuloId==id,ct);
            if(foto==null) return NotFound();
            Response.Headers["X-Content-Type-Options"]="nosniff";
            Response.Headers.CacheControl="no-store";
            return File(foto.Contenido,foto.Mime);
        }
    }
}
