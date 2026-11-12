using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SegundaOportunidad.Services;

namespace SegundaOportunidad.Controllers;
[Route("api/ml")]
[ResponseCache(Location=ResponseCacheLocation.None,NoStore=true)]
public sealed class MlController(MlRecommendationService service):Controller
{
    [HttpGet("recomendaciones")]
    public async Task<IActionResult> Recommendations(string? consulta,string? articuloId,int cantidad=6,CancellationToken ct=default)
    {
        if(consulta?.Length>300||articuloId?.Length>100||cantidad is <1 or >12)return BadRequest(new {error="Filtros inválidos; máximo 12 recomendaciones."});
        var result=await service.RecommendAsync(User.FindFirstValue(ClaimTypes.NameIdentifier),consulta,articuloId,cantidad,ct);
        return result.Disponible?Ok(result):StatusCode(503,result);
    }
}
