using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SegundaOportunidad.Services;

namespace SegundaOportunidad.Controllers;

public sealed class AdminMlViewModel
{
    public string Consulta { get; set; } = "libros";
    public string Texto { get; set; } = "Libro usado para estudiar, con fotos del estado real.";
    public MlNetClassification? Clasificacion { get; set; }
    public MlAnswer? Recomendaciones { get; set; }
}

[Authorize(Roles = "Admin")]
[AutoValidateAntiforgeryToken]
public sealed class AdminMlController(MlNetModels classifier, MlNetRecommendationService recommender) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = new AdminMlViewModel();
        model.Clasificacion = classifier.Classify(model.Texto);
        model.Recomendaciones = await recommender.RecommendAsync(model.Consulta, 6, ct);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Index(string? texto, string? consulta, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(texto) || texto.Length > 2000 || string.IsNullOrWhiteSpace(consulta) || consulta.Length > 300)
            return BadRequest("Escribe un texto de hasta 2000 caracteres y una búsqueda de hasta 300.");
        return View(new AdminMlViewModel { Texto = texto, Consulta = consulta,
            Clasificacion = classifier.Classify(texto), Recomendaciones = await recommender.RecommendAsync(consulta, 6, ct) });
    }
}
