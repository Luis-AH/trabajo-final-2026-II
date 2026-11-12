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
        public async Task<IActionResult> Index(string searchString, int? categoriaId, int pagina=1, decimal? precioMax=null)
        {
            if(pagina is <1 or >10000 || searchString?.Length>150 || precioMax<0)return BadRequest("Filtros inválidos.");
            var query = ChatCatalogAccess.Visible(_context,DateTime.UtcNow).Include(a => a.Categoria).Include(a => a.User).AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                // Algolia es una optimización; no depender de su disponibilidad para buscar.
                var ids = await _algoliaService.SearchArticulosIdsAsync(searchString);
                var term=searchString.Trim().ToLower();
                query = query.Where(a => ids.Contains(a.Id) || a.Title.ToLower().Contains(term) || a.Description.ToLower().Contains(term));
            }

            if (categoriaId.HasValue)
            {
                query = query.Where(a => a.CategoriaId == categoriaId.Value);
            }

            if(precioMax!=null)query=query.Where(a=>a.Modalidad==ModalidadArticulo.Venta&&a.Price!=null&&a.Price<=precioMax);
            ViewData["Total"]=await query.CountAsync();ViewData["Pagina"]=pagina;
            ViewData["Categorias"] = new SelectList(_context.Categorias.OrderBy(c => c.Name=="Otros...").ThenBy(c => c.Name), "Id", "Name",categoriaId);
            return View(await query.OrderByDescending(a=>a.CreatedAt).ThenBy(a=>a.Id).Skip((pagina-1)*12).Take(12).ToListAsync());
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
        public IActionResult Explorar()=>RedirectToAction(nameof(Index));

        [HttpGet]
        public async Task<IActionResult> Edit(string id,CancellationToken ct)
        {
            var owner=_userManager.GetUserId(User);
            var article=await _context.Articulos.SingleOrDefaultAsync(a=>a.Id==id&&a.UserId==owner,ct);
            if(article==null)return NotFound();
            ViewData["CategoriaId"]=new SelectList(await _context.Categorias.OrderBy(c=>c.Name).ToListAsync(ct),"Id","Name",article.CategoriaId);
            return View(article);
        }
        [HttpPost,ValidateAntiForgeryToken,RequestSizeLimit(5*1024*1024)]
        public async Task<IActionResult> Edit(string id,[Bind("Title,Description,Modalidad,EstadoFisico,Price,ExchangeDetails,CategoriaId,ModeracionVersion")] Articulo input,IFormFile? imagen,CancellationToken ct)
        {
            var owner=_userManager.GetUserId(User);
            var article=await _context.Articulos.SingleOrDefaultAsync(a=>a.Id==id&&a.UserId==owner,ct);
            if(article==null)return NotFound();
            ModelState.Remove("UserId");ModelState.Remove("User");ModelState.Remove("Categoria");
            if(!await _context.Categorias.AnyAsync(c=>c.Id==input.CategoriaId,ct))ModelState.AddModelError("CategoriaId","Selecciona una categoría válida.");
            FotoArticulo? photo=null;
            if(imagen!=null&&imagen.Length>0)try {photo=await ArticuloPhotoService.LeerAsync(imagen,ct);}catch(ArgumentException ex){ModelState.AddModelError("imagen",ex.Message);}
            if(ModelState.IsValid)
            {
                _context.Entry(article).Property(a=>a.ModeracionVersion).OriginalValue=input.ModeracionVersion;
                article.Title=input.Title;article.Description=input.Description;article.Modalidad=input.Modalidad;article.EstadoFisico=input.EstadoFisico;
                article.Price=input.Price;article.ExchangeDetails=input.ExchangeDetails;article.CategoriaId=input.CategoriaId;article.ModeracionVersion=Guid.NewGuid().ToString();
                if(photo!=null)
                {
                    var old=await _context.FotosArticulos.SingleOrDefaultAsync(p=>p.ArticuloId==id,ct);
                    if(old==null){photo.ArticuloId=id;_context.FotosArticulos.Add(photo);}else{old.Contenido=photo.Contenido;old.Mime=photo.Mime;}
                    article.ImageUrl="/Articulos/Imagen/"+id;
                }
                try {await _context.SaveChangesAsync(ct);await _algoliaService.IndexArticuloAsync(article);TempData["Success"]="Publicación actualizada.";return RedirectToAction(nameof(Details),new {id});}
                catch(DbUpdateConcurrencyException){ModelState.AddModelError("","La publicación cambió mientras editabas. Recarga y revisa los cambios.");}
            }
            ViewData["CategoriaId"]=new SelectList(await _context.Categorias.OrderBy(c=>c.Name).ToListAsync(ct),"Id","Name",input.CategoriaId);
            input.Id=id;return View(input);
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
