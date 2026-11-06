using Microsoft.AspNetCore.Mvc;
using SegundaOportunidad.Services;

namespace SegundaOportunidad.Controllers
{
    [Route("api/chat")]
    public class ChatController : Controller
    {
        private readonly AiSearchService _aiSearchService;

        public ChatController(AiSearchService aiSearchService)
        {
            _aiSearchService = aiSearchService;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] ChatRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.MensajeUsuario))
                return BadRequest(new { error = "El mensaje es obligatorio." });

            var respuesta = await _aiSearchService.GetCompletionAsync(
                "Eres el asistente virtual de SegundaOportunidad, una plataforma de intercambio y publicación de artículos. Responde en español, de forma breve y útil. No inventes publicaciones ni disponibilidad; orienta al usuario a explorar los artículos de la plataforma cuando pregunte por productos específicos.",
                request.MensajeUsuario.Trim());

            return Ok(new { respuesta });
        }

        public sealed class ChatRequest
        {
            public string MensajeUsuario { get; set; } = string.Empty;
        }
    }
}
