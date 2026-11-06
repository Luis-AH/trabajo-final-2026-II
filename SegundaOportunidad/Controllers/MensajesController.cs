using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;
using SegundaOportunidad.Services;

namespace SegundaOportunidad.Controllers
{
    [Authorize]
    public class MensajesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RabbitMqPublisher _rabbitMqPublisher;

        public MensajesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, RabbitMqPublisher rabbitMqPublisher)
        {
            _context = context;
            _userManager = userManager;
            _rabbitMqPublisher = rabbitMqPublisher;
        }

        public async Task<IActionResult> Index(string? userId = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();
            if (!string.IsNullOrEmpty(userId) && !await _context.ApprovedParticipants().AnyAsync(u => u.Id == userId))
                return BadRequest("Solo puedes conversar con usuarios aprobados.");

            // Lista de contactos (usuarios con los que se ha intercambiado o algo así)
            var users = await _context.ApprovedParticipants()
                .Where(u => u.Id != currentUser.Id)
                .ToListAsync();

            ViewBag.Contacts = users;
            ViewBag.CurrentUser = currentUser;
            ViewBag.CurrentUserId = currentUser.Id;
            ViewBag.ActiveChatUserId = userId;

            if (!string.IsNullOrEmpty(userId))
            {
                var messages = await _context.Mensajes
                    .Where(m => (m.SenderId == currentUser.Id && m.ReceiverId == userId) || 
                                (m.SenderId == userId && m.ReceiverId == currentUser.Id))
                    .OrderBy(m => m.SentAt)
                    .ToListAsync();
                
                ViewBag.Messages = messages;
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();
            var ahora = DateTime.UtcNow;
            if (dto.ReceiverId == currentUser.Id || !await _context.ApprovedParticipants().AnyAsync(u => u.Id == dto.ReceiverId && (u.AccountSuspendedUntilUtc == null || u.AccountSuspendedUntilUtc <= ahora)))
                return BadRequest("El destinatario debe estar aprobado y sin suspensión de cuenta.");

            if (string.IsNullOrEmpty(dto.ReceiverId) || string.IsNullOrEmpty(dto.Content))
            {
                return BadRequest("ReceiverId y Content son obligatorios.");
            }

            var messageDto = new
            {
                RabbitMqMessageId = Guid.NewGuid().ToString(),
                Content = dto.Content,
                SenderId = currentUser.Id,
                ReceiverId = dto.ReceiverId,
                SentAt = DateTime.UtcNow
            };

            try { await _rabbitMqPublisher.PublishMessageAsync("chat_messages", messageDto); }
            catch (MessagingUnavailableException) { return StatusCode(503, new { error = "La mensajería no está disponible temporalmente. Inténtalo más tarde." }); }

            return Ok(new { success = true, messageId = messageDto.RabbitMqMessageId });
        }

        public class SendMessageDto
        {
            public string ReceiverId { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
        }
    }
}
