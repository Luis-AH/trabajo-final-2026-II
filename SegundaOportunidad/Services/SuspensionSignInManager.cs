using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SegundaOportunidad.Models;

namespace SegundaOportunidad.Services;
public sealed class SuspensionSignInManager(
    UserManager<ApplicationUser> users, IHttpContextAccessor accessor,
    IUserClaimsPrincipalFactory<ApplicationUser> claims, IOptions<IdentityOptions> options,
    ILogger<SignInManager<ApplicationUser>> logger, IAuthenticationSchemeProvider schemes,
    IUserConfirmation<ApplicationUser> confirmation, TimeProvider reloj)
    : SignInManager<ApplicationUser>(users, accessor, claims, options, logger, schemes, confirmation)
{
    public override async Task<bool> CanSignInAsync(ApplicationUser user) =>
        !(user.AccountSuspendedUntilUtc > reloj.GetUtcNow().UtcDateTime) && await base.CanSignInAsync(user);
}
