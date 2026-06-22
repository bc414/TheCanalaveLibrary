using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Bakes the hot <see cref="User"/> columns <see cref="IActiveUserContext"/> needs
/// (<see cref="User.ShowMatureContent"/>, theme name, <see cref="User.PrefersAnimatedSprites"/>) into the
/// auth cookie's claims at sign-in — settled WU12. This is what lets <c>ServerActiveUserContext</c> read
/// purely from claims with zero DbContext dependency, avoiding a circular dependency with
/// <c>ApplicationDbContext</c>'s content-rating query filter (which itself depends on
/// <see cref="IActiveUserContext"/>). Role claims (<c>IsModerator</c>/<c>IsAdmin</c>) need no custom
/// work — they already come for free from the base <see cref="UserClaimsPrincipalFactory{TUser,TRole}"/>
/// once roles are configured (<c>.AddRoles&lt;ApplicationRole&gt;()</c> in Program.cs).
/// </summary>
/// <remarks>
/// Consequence: if a user's ShowMatureContent/Theme/PrefersAnimatedSprites changes (WU30 profile
/// settings), the auth cookie is stale until next sign-in unless that write path calls
/// <c>SignInManager.RefreshSignInAsync</c> to reissue claims. Flagged here for WU30, not solved by WU12.
/// </remarks>
public class ApplicationUserClaimsPrincipalFactory(
    UserManager<User> userManager,
    RoleManager<ApplicationRole> roleManager,
    IOptions<IdentityOptions> options,
    ReadOnlyApplicationDbContext readDb)
    : UserClaimsPrincipalFactory<User, ApplicationRole>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
    {
        ClaimsIdentity identity = await base.GenerateClaimsAsync(user);

        // Themes carry no query filter, so this read is unaffected by whatever the signing-in user's
        // *previous* IActiveUserContext happened to be.
        string themeName = await readDb.Themes
            .Where(t => t.ThemeId == user.ThemeId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync() ?? "Pokémon";

        identity.AddClaim(new Claim(ActiveUserClaimTypes.ShowMatureContent, user.ShowMatureContent.ToString()));
        identity.AddClaim(new Claim(ActiveUserClaimTypes.Theme, themeName));
        identity.AddClaim(new Claim(ActiveUserClaimTypes.PrefersAnimatedSprites, user.PrefersAnimatedSprites.ToString()));

        return identity;
    }
}
