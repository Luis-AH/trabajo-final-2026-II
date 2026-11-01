using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;
using SegundaOportunidad.Services;

namespace SegundaOportunidad.Controllers;
[Authorize(Roles="Admin"),AutoValidateAntiforgeryToken]
public sealed class AdminGuardController(ApplicationDbContext db,AdminGuardService service) : Controller
{
    private static string SearchPattern(string text) => "%"+text.Replace("\\","\\\\").Replace("%","\\%").Replace("_","\\_")+"%";
    [HttpGet]
    public async Task<IActionResult> Index(TipoConsultaAdminGuard? tipo,string? objetivoId,CancellationToken ct)
    {
        if(tipo.HasValue) return Destino(tipo,objetivoId);
        return View(await Panel(new(),null,ct));
    }
    private IActionResult Destino(TipoConsultaAdminGuard? tipo,string? id) => tipo switch {
        TipoConsultaAdminGuard.Estadisticas => RedirectToAction(nameof(Datos)),
        TipoConsultaAdminGuard.Publicacion when !string.IsNullOrEmpty(id) => RedirectToAction(nameof(Analizar),new { id }),
        TipoConsultaAdminGuard.Publicacion => RedirectToAction(nameof(Publicaciones)),
        TipoConsultaAdminGuard.Reporte when !string.IsNullOrEmpty(id) => RedirectToAction(nameof(Resumen),new { id }),
        TipoConsultaAdminGuard.Reporte => RedirectToAction(nameof(Reportes)),
        _ => RedirectToAction(nameof(Index))
    };
    [HttpGet,ActionName("Consultar")]
    public IActionResult AbrirConsulta(TipoConsultaAdminGuard? tipo,string? objetivoId) => Destino(tipo,objetivoId);
    [HttpGet]
    public async Task<IActionResult> Datos(CancellationToken ct) => View(await Panel(new() { Tipo=TipoConsultaAdminGuard.Estadisticas,Pregunta="¿Qué indican los datos globales y qué revisiones conviene priorizar?" },null,ct));
    [HttpGet]
    public async Task<IActionResult> Publicaciones(string? busqueda,EstadoModeracion? estado,int pagina=1,CancellationToken ct=default)
    {
        if(!ModelState.IsValid || busqueda?.Length>150 || pagina is < 1 or > 100000 || (estado.HasValue && !Enum.IsDefined(estado.Value))) return BadRequest("Filtros inválidos.");
        busqueda=busqueda?.Trim();
        var query=db.Articulos.IgnoreQueryFilters().AsNoTracking();
        if(!string.IsNullOrEmpty(busqueda)) { var pattern=SearchPattern(busqueda); query=query.Where(a => EF.Functions.Like(a.Title,pattern,"\\")); }
        if(estado.HasValue) query=query.Where(a => a.Moderacion==estado);
        var model=new GuardGalleryViewModel { Busqueda=busqueda,Estado=estado,Total=await query.CountAsync(ct) };
        model.Pagina=Math.Min(pagina,model.Paginas);
        model.Publicaciones=await query.OrderByDescending(a => a.CreatedAt).ThenBy(a => a.Id).Skip((model.Pagina-1)*12).Take(12)
            .Select(a => new GuardPublicationCard(a.Id,a.Title,a.Description.Length>160 ? a.Description.Substring(0,160)+"…" : a.Description,a.ImageUrl,a.Categoria!.Name,a.Moderacion,a.CreatedAt)).ToListAsync(ct);
        return View(model);
    }
    [HttpGet]
    public async Task<IActionResult> Analizar(string id,CancellationToken ct)
    {
        var model=await Panel(new() { Tipo=TipoConsultaAdminGuard.Publicacion,ObjetivoId=id,Pregunta="Revisa si la foto, el título y la descripción son coherentes. Señala riesgos o inconsistencias, qué evidencia falta y si requiere revisión humana. No certifiques legitimidad ni declares fraude sin pruebas." },null,ct);
        return model.Publicacion==null ? View("ErrorConsulta",model) : View(model);
    }
    [HttpGet]
    public async Task<IActionResult> Reportes(string? busqueda,EstadoReporte? estado,int pagina=1,CancellationToken ct=default)
    {
        if(!ModelState.IsValid || busqueda?.Length>150 || pagina is < 1 or > 100000 || (estado.HasValue && !Enum.IsDefined(estado.Value))) return BadRequest("Filtros inválidos.");
        busqueda=busqueda?.Trim();
        var query=db.Reportes.AsNoTracking();
        if(!string.IsNullOrEmpty(busqueda)) { var pattern=SearchPattern(busqueda); query=query.Where(r => EF.Functions.Like(r.Descripcion,pattern,"\\") || EF.Functions.Like(r.Id,pattern,"\\")); }
        if(estado.HasValue) query=query.Where(r => r.Estado==estado);
        var model=new GuardReportsViewModel { Busqueda=busqueda,Estado=estado,Total=await query.CountAsync(ct) };
        model.Pagina=Math.Min(pagina,model.Paginas);
        model.Reportes=await query.OrderByDescending(r => r.CreadoUtc).ThenBy(r => r.Id).Skip((model.Pagina-1)*12).Take(12).ToListAsync(ct);
        return View(model);
    }
    [HttpGet]
    public async Task<IActionResult> Resumen(string id,CancellationToken ct)
    {
        var model=await Panel(new() { Tipo=TipoConsultaAdminGuard.Reporte,ObjetivoId=id,Pregunta="Resume los hechos denunciados y los antecedentes agregados. Distingue alegaciones de evidencia y sugiere verificaciones y prioridad de revisión." },null,ct);
        return model.ReporteActual==null ? View("ErrorConsulta",model) : View(model);
    }
    private async Task<PanelAdminGuardViewModel> Panel(ConsultaAdminGuardViewModel input,string? error,CancellationToken ct)
    {
        var admin=User.FindFirstValue(ClaimTypes.NameIdentifier);
        var history=db.AnalisisAdminGuard.AsNoTracking().Where(a => a.AdministradorId==admin);
        if(input.Tipo.HasValue) history=history.Where(a => a.Tipo==input.Tipo);
        if(input.Tipo==TipoConsultaAdminGuard.Publicacion) history=history.Where(a => a.ArticuloId==input.ObjetivoId);
        if(input.Tipo==TipoConsultaAdminGuard.Reporte) history=history.Where(a => a.ReporteId==input.ObjetivoId);
        var model=new PanelAdminGuardViewModel { Configurado=service.Configurado,Consulta=input,Error=error,
            Historial=await history.OrderByDescending(a => a.CreadoUtc).Take(20).ToListAsync(ct) };
        if(input.Tipo==TipoConsultaAdminGuard.Publicacion)
            model.Publicacion=await db.Articulos.IgnoreQueryFilters().AsNoTracking().Include(a => a.Categoria).SingleOrDefaultAsync(a => a.Id==input.ObjetivoId,ct);
        if(input.Tipo==TipoConsultaAdminGuard.Reporte)
        {
            model.ReporteActual=await db.Reportes.AsNoTracking().SingleOrDefaultAsync(r => r.Id==input.ObjetivoId,ct);
            if(model.ReporteActual!=null)
            {
                var userId=model.ReporteActual.DenunciadoId;
                model.Antecedentes=await db.Reportes.CountAsync(r => r.DenunciadoId==userId && r.Id!=input.ObjetivoId,ct);
                model.DecisionesSanciones=await db.SancionesUsuarios.CountAsync(s => s.UsuarioId==userId,ct);
            }
        }
        return model;
    }
    private async Task<IActionResult> MostrarError(ConsultaAdminGuardViewModel input,string message,CancellationToken ct)
    {
        var model=await Panel(input,message,ct);
        var view=input.Tipo switch {
            TipoConsultaAdminGuard.Estadisticas => "Datos",
            TipoConsultaAdminGuard.Publicacion when model.Publicacion!=null => "Analizar",
            TipoConsultaAdminGuard.Reporte when model.ReporteActual!=null => "Resumen",
            _ => "ErrorConsulta"
        };
        return View(view,model);
    }
    [HttpPost]
    public async Task<IActionResult> Consultar(ConsultaAdminGuardViewModel input,CancellationToken ct)
    {
        if(!ModelState.IsValid) return await MostrarError(input,"Revisa el objetivo y la pregunta.",ct);
        var admin=User.FindFirstValue(ClaimTypes.NameIdentifier);
        if(admin==null) return Forbid();
        input.ObjetivoId=input.ObjetivoId?.Trim();
        var exists=input.Tipo switch {
            TipoConsultaAdminGuard.Publicacion => await db.Articulos.IgnoreQueryFilters().AnyAsync(a => a.Id==input.ObjetivoId,ct),
            TipoConsultaAdminGuard.Reporte => await db.Reportes.AnyAsync(r => r.Id==input.ObjetivoId,ct),
            TipoConsultaAdminGuard.Estadisticas => true,
            _ => false
        };
        if(!exists) return await MostrarError(input,"El elemento no está disponible. Selecciona una publicación o reporte existente desde su página de búsqueda.",ct);
        try { var id=await service.ConsultarAsync(admin,input,ct); return RedirectToAction(nameof(Resultado),new {id}); }
        catch(KeyNotFoundException) { return await MostrarError(input,"El elemento ya no está disponible. Vuelve a buscarlo.",ct); }
        catch(Exception ex) when(ex is ArgumentException or InvalidOperationException or HttpRequestException or System.Text.Json.JsonException or OperationCanceledException)
        {
            var message=ex is ArgumentException or InvalidOperationException ? ex.Message : "No se pudo consultar Groq. Comprueba conexión, modelo y cuotas. No se aplicó ninguna sanción.";
            return await MostrarError(input,message,CancellationToken.None);
        }
    }
    [HttpGet]
    public async Task<IActionResult> Resultado(string id,CancellationToken ct)
    {
        var admin=User.FindFirstValue(ClaimTypes.NameIdentifier);
        var audit=await db.AnalisisAdminGuard.AsNoTracking().SingleOrDefaultAsync(a => a.Id==id && a.AdministradorId==admin,ct);
        return audit==null ? NotFound() : View(audit);
    }
}
