using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SegundaOportunidad.Services;

namespace SegundaOportunidad.Controllers;
[Route("api/ml")]
[ResponseCache(Location=ResponseCacheLocation.None,NoStore=true)]
public sealed class MlController(MlRecommendationService service):Controller
{
    [HttpGet("recomendaciones-mlnet")]
    public async Task<IActionResult> MlNet([FromServices] MlNetRecommendationService recommender,
        string? consulta, int cantidad = 6, CancellationToken ct = default)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(consulta) || consulta.Length > 300 || cantidad is < 1 or > 12)
            return BadRequest(new { error = "Escribe una consulta de hasta 300 caracteres y solicita entre 1 y 12 artículos." });
        return Ok(await recommender.RecommendAsync(consulta, cantidad, ct));
    }

    [HttpGet("recomendaciones")]
    public async Task<IActionResult> Recommendations(string? consulta,string? articuloId,int cantidad=6,int rotacion=0,CancellationToken ct=default)
    {
        if(!ModelState.IsValid||consulta?.Length>300||articuloId?.Length>100||cantidad is <1 or >12||rotacion is <0 or >1000000)return BadRequest(new {error="Filtros inválidos; máximo 12 recomendaciones."});
        var result=await service.RecommendAsync(User.FindFirstValue(ClaimTypes.NameIdentifier),consulta,articuloId,cantidad,ct,rotacion);
        return result.Disponible?Ok(result):StatusCode(503,result);
    }
}
