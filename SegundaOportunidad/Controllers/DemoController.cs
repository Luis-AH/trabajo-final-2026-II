using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SegundaOportunidad.Data;

namespace SegundaOportunidad.Controllers;
[Authorize(Roles="Admin"),AutoValidateAntiforgeryToken]
public sealed class DemoController(IWebHostEnvironment environment,DemoCatalogSeeder seeder):Controller
{
    [HttpGet] public IActionResult Index()
    {
        if(!environment.IsDevelopment()&&!environment.IsEnvironment("Demo"))return NotFound();
        Response.Headers.CacheControl="no-store";
        return View();
    }
    [HttpPost] public async Task<IActionResult> Preparar(CancellationToken ct)
    {
        if(!environment.IsDevelopment()&&!environment.IsEnvironment("Demo"))return NotFound();
        Response.Headers.CacheControl="no-store";Response.Headers["Referrer-Policy"]="no-referrer";
        try {return View("Index",await seeder.SeedAsync(ct));}
        catch(DemoSeedSafetyException ex)
        {
            Response.StatusCode=StatusCodes.Status409Conflict;
            ModelState.AddModelError("",ex.Message);
            return View("Index");
        }
    }
}
