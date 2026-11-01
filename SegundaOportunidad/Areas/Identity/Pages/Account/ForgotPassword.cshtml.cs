using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace SegundaOportunidad.Areas.Identity.Pages.Account;
[AllowAnonymous]
public sealed class ForgotPasswordRedirectModel : PageModel
{
    public IActionResult OnGet() => RedirectToAction("Recuperar","Seguridad",new { area="" });
    public IActionResult OnPost() => RedirectToAction("Recuperar","Seguridad",new { area="" });
}
