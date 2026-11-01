
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;

namespace SegundaOportunidad.Controllers
{
    [Authorize]
    public class IntercambiosController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public IntercambiosController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Solicitar intercambio
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Proponer(
            string offeredArticleId,
            string requestedArticleId)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
                return Challenge();

            if (offeredArticleId == requestedArticleId)
                return BadRequest("No puedes intercambiar el mismo artículo.");

            var offered = await _context.Articulos
                .FirstOrDefaultAsync(a =>
                    a.Id == offeredArticleId &&
                    a.UserId == userId &&
                    a.IsActive);

            var requested = await _context.Articulos
                .FirstOrDefaultAsync(a =>
                    a.Id == requestedArticleId &&
                    a.IsActive);

            if (offered == null || requested == null)
                return BadRequest("Los artículos no están disponibles.");

            if (requested.UserId == userId)
                return BadRequest("No puedes solicitar tus propios artículos.");

            var existe = await _context.Intercambios.AnyAsync(i =>
                i.ProposerId == userId &&
                i.OfferedArticleId == offeredArticleId &&
                i.RequestedArticleId == requestedArticleId &&
                i.Estado == EstadoIntercambio.Pendiente);

            if (existe)
                return BadRequest("Ya tienes una solicitud pendiente.");

            var intercambio = new Intercambio
            {
                ProposerId = userId,
                ReceiverId = requested.UserId,
                OfferedArticleId = offeredArticleId,
                RequestedArticleId = requestedArticleId,
                Estado = EstadoIntercambio.Pendiente
            };

            _context.Intercambios.Add(intercambio);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Solicitud de intercambio enviada.";

            return RedirectToAction("Intercambios", "Perfil");
        }

        // Aceptar solicitud
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Aceptar(string id)
        {
            var userId = _userManager.GetUserId(User);

            var intercambio = await _context.Intercambios
                .FirstOrDefaultAsync(i =>
                    i.Id == id &&
                    i.ReceiverId == userId);

            if (intercambio == null)
                return NotFound();

            if (intercambio.Estado != EstadoIntercambio.Pendiente)
                return BadRequest("La solicitud ya fue procesada.");

            intercambio.Estado = EstadoIntercambio.Aceptado;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Intercambio aceptado.";

            return RedirectToAction("Intercambios", "Perfil");
        }

        // Rechazar solicitud
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rechazar(string id)
        {
            var userId = _userManager.GetUserId(User);

            var intercambio = await _context.Intercambios
                .FirstOrDefaultAsync(i =>
                    i.Id == id &&
                    i.ReceiverId == userId);

            if (intercambio == null)
                return NotFound();

            if (intercambio.Estado != EstadoIntercambio.Pendiente)
                return BadRequest("La solicitud ya fue procesada.");

            intercambio.Estado = EstadoIntercambio.Rechazado;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Intercambio rechazado.";

            return RedirectToAction("Intercambios", "Perfil");
        }

        // Confirmar que se realizó el intercambio
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Completar(string id)
        {
            var userId = _userManager.GetUserId(User);

            var intercambio = await _context.Intercambios
                .FirstOrDefaultAsync(i =>
                    i.Id == id &&
                    (i.ProposerId == userId ||
                     i.ReceiverId == userId));

            if (intercambio == null)
                return NotFound();

            if (intercambio.Estado != EstadoIntercambio.Aceptado)
                return BadRequest("El intercambio debe estar aceptado.");

            intercambio.Estado = EstadoIntercambio.Completado;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Intercambio completado correctamente.";

            return RedirectToAction("Intercambios", "Perfil");
        }
    }
}
