using Microsoft.AspNetCore.Mvc;
using SegundaOportunidad.Services;

namespace SegundaOportunidad.Controllers;

[Route("api/chat")]
public class ChatController(ChatAssistantService assistant) : Controller
{
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ChatRequest? request)
    {
        if(request==null || string.IsNullOrWhiteSpace(request.MensajeUsuario))
            return BadRequest(new {error="El mensaje es obligatorio."});
        if(!ModelState.IsValid || request.MensajeUsuario.Length>1000)
            return BadRequest(new {error="El mensaje debe contener hasta 1000 caracteres."});
        return Ok(await assistant.ReplyAsync(request.MensajeUsuario.Trim(),HttpContext.RequestAborted));
    }
    public sealed class ChatRequest
    {
        public string MensajeUsuario {get;set;}="";
    }
}