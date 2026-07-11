using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Overrides <see cref="SignInManager{TUser}.CanSignInAsync"/> to enforce <see cref="User.AccountStatus"/>
/// at the single choke point every sign-in path funnels through — WU38a (account-status login
/// enforcement, folded in from the deferred follow-up noted in <c>workplan.md</c> after WU39).
///
/// <para><b>Why this method, not each page.</b> <see cref="SignInManager{TUser}.PasswordSignInAsync"/>,
/// <c>PasskeySignInAsync</c>, <c>TwoFactorSignInAsync</c>, and <c>ExternalLoginSignInAsync</c> (Login,
/// LoginWith2fa, ExternalLogin, PasskeySubmit) all route through <c>PreSignInCheck</c>, which calls
/// <see cref="CanSignInAsync"/> before anything else. Overriding it here blocks every path in one place;
/// a blocked attempt surfaces uniformly as <see cref="SignInResult.NotAllowed"/> (see <c>Login.razor</c>
/// for the suspended/banned reason message built from that result).</para>
///
/// <para><b>Suspended expiry is read-only here.</b> Once <see cref="User.SuspendedUntilUtc"/> is in the
/// past, sign-in is allowed again — this method does not write back to <c>AccountStatus</c> (no lazy
/// restore at the choke point); the row keeps reading <c>Suspended</c> until a moderator changes it.</para>
///
/// <para><b>Active sessions are not touched here.</b> Killing an already-open session when a user is
/// Suspended/Banned is a separate mechanism — a security-stamp bump in
/// <c>ServerModerationWriteService.ApplyAccountActionAsync</c> — that rides the existing 30-minute
/// <c>IdentityRevalidatingAuthenticationStateProvider</c> revalidation. See
/// <c>canalave-conventions/security.md</c> "Account-Status Enforcement" for the full mechanism.</para>
/// </summary>
public class CanalaveSignInManager(
    UserManager<User> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<User> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<User>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<User> confirmation)
    : SignInManager<User>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
    public override async Task<bool> CanSignInAsync(User user)
    {
        if (user.AccountStatus == AccountStatusEnum.Banned)
            return false;

        if (user.AccountStatus == AccountStatusEnum.Suspended
            && user.SuspendedUntilUtc is { } suspendedUntil
            && suspendedUntil > DateTime.UtcNow)
        {
            return false;
        }

        return await base.CanSignInAsync(user);
    }
}
