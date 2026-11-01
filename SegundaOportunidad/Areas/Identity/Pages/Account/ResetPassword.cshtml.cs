using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace SegundaOportunidad.Areas.Identity.Pages.Account;
[AllowAnonymous]
public sealed class ResetPasswordRedirectModel : PageModel
{
    public IActionResult OnGet(string? email,string? code) => string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code)
        ? RedirectToAction("Recuperar","Seguridad",new { area="" })
        : RedirectToAction("Restablecer","Seguridad",new { area="",email,codigo=code });
    public IActionResult OnPost() => RedirectToAction("Recuperar","Seguridad",new { area="" });
}
