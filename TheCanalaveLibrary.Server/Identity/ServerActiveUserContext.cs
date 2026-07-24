using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Scoped, claims-only implementation of <see cref="IActiveUserContext"/> — settled WU12. Reads
/// exclusively from a <see cref="ClaimsPrincipal"/>, with no DbContext dependency; see
/// <see cref="ApplicationUserClaimsPrincipalFactory"/> for what bakes ShowMatureContent/Theme/
/// PrefersAnimatedSprites into those claims at sign-in. Role claims (Moderator/Admin) come from
/// ASP.NET Core Identity's own role-claims support, no custom wiring needed. This claims-only design is
/// what lets <c>ApplicationDbContext</c> take <see cref="IActiveUserContext"/> as a constructor
/// dependency for its content-rating query filter without creating a circular dependency back through
/// a DbContext.
///
/// <see cref="IActiveUserContext"/> is constructed in more DI scopes than just Blazor circuits —
/// minimal-API request scopes and manually-created background scopes (e.g. <c>DataSeeder</c>'s startup
/// scope) also resolve a scoped <c>ApplicationDbContext</c>, and neither has a live Blazor component
/// tree. <see cref="AuthenticationStateProvider"/> is part of that component-rendering pipeline — it's
/// the wrong tool outside it, and querying it from a scope with no circuit risks throwing or never
/// completing. <see cref="IHttpContextAccessor"/> is therefore the PRIMARY source (synchronous, always
/// safe, already populated by the auth middleware for any real HTTP request — minimal-API calls and a
/// Blazor circuit's own initial/prerender request both have one); <c>AuthenticationStateProvider</c> is
/// only consulted as a fallback for genuine SignalR-only postbacks after the initiating HTTP request has
/// completed, where no <c>HttpContext</c> exists but the circuit's auth state is already established.
///
/// <b>Resolution is lazy, not eager in the constructor (found verifying WU12 — see ImageStorage/Stories
/// audit notes).</b> ASP.NET Core Identity's own <c>SecurityStampValidator</c> runs as part of the
/// authentication middleware itself and resolves a scoped <c>ApplicationDbContext</c> (hence
/// <c>IActiveUserContext</c>) <i>during</i> cookie validation — before <c>HttpContext.User</c> is the
/// final principal for the rest of the request. An eager constructor read captured that early, still-
/// anonymous snapshot and never updated, even though every later consumer's own direct read of
/// <c>HttpContext.User</c> showed the correct claims. Deferring the read to first property access avoids
/// this: nothing in this app's services touches a query filter (or any other property here) until well
/// after the full middleware pipeline has completed.
/// </summary>
public class ServerActiveUserContext(
    IHttpContextAccessor httpContextAccessor,
    AuthenticationStateProvider authenticationStateProvider) : IActiveUserContext
{
    private ClaimsPrincipal? _principal;
    private AnonPrefs? _anonPrefs;

    private ClaimsPrincipal Principal => _principal ??= ResolvePrincipal(httpContextAccessor, authenticationStateProvider);

    // Anonymous prefs cookie (WU-AccessGate) — same lazy-once discipline as Principal: never read
    // in the constructor (the SecurityStampValidator early-capture hazard in the type doc comment
    // applies to any constructor-time HttpContext read), captured on first access for the life of
    // the scope. Circuit dispatches with no HttpContext keep the value captured at circuit start;
    // consent actions force a full-document navigation anyway (frozen-circuit rule).
    private AnonPrefs AnonPrefsValue => _anonPrefs ??=
        httpContextAccessor.HttpContext is { } ctx ? AnonPrefs.Read(ctx.Request) : AnonPrefs.Empty;

    public bool IsAuthenticated => Principal.Identity?.IsAuthenticated ?? false;

    public int? UserId =>
        IsAuthenticated && int.TryParse(Principal.FindFirstValue(ClaimTypes.NameIdentifier), out int id)
            ? id
            : null;

    // Authenticated: the claim baked at sign-in (reissued on change via RefreshSignInAsync —
    // MA-605 closed by WU-AccessGate). Anonymous: the prefs-cookie mature toggle (Fimfiction/AO3
    // model) — which also feeds the GroupAudience filter, by design.
    public bool ShowMatureContent =>
        IsAuthenticated
            ? bool.TryParse(Principal.FindFirstValue(ActiveUserClaimTypes.ShowMatureContent), out bool mature) && mature
            : AnonPrefsValue.Mature;

    // ANONYMOUS per-item consent only — authenticated reveals are DB rows checked by RevealCheck
    // in read services (this context stays DbContext-free; see IActiveUserContext doc).
    public bool HasAnonRevealed(RevealedEntityType entityType, int entityId) =>
        !IsAuthenticated && AnonPrefsValue.HasRevealed(entityType, entityId);

    // Set by VerifiedBotMiddleware (Phase 5) when Seo:TrustVerifiedBots is enabled and the edge
    // signal verifies; absent middleware or config → false. Crawlers only ever hit the
    // SSR/prerender pass, which always has an HttpContext.
    public bool IsVerifiedBot =>
        httpContextAccessor.HttpContext?.Items.TryGetValue(VerifiedBotMiddleware.ItemKey, out object? v) == true
        && v is true;

    // Returns the URL-safe slug (e.g. "pokemon") — not the display name. Default matches the
    // seeded Theme.Slug so anonymous users resolve optimistic sprite URLs to the correct path.
    public string Theme => Principal.FindFirstValue(ActiveUserClaimTypes.Theme) ?? "pokemon";

    public bool PrefersAnimatedSprites =>
        !IsAuthenticated
        || !bool.TryParse(Principal.FindFirstValue(ActiveUserClaimTypes.PrefersAnimatedSprites), out bool animated)
        || animated;

    public bool IsModerator => Principal.IsInRole("Moderator");
    public bool IsAdmin => Principal.IsInRole("Admin");

    private static ClaimsPrincipal ResolvePrincipal(
        IHttpContextAccessor httpContextAccessor,
        AuthenticationStateProvider authenticationStateProvider)
    {
        // Primary source: a real HttpContext already has its User populated synchronously by the auth
        // middleware — true for minimal-API requests, DataSeeder has none (falls through), and a
        // Blazor circuit's own initial/prerender request.
        ClaimsPrincipal? httpUser = httpContextAccessor.HttpContext?.User;
        if (httpUser?.Identity is not null)
        {
            return httpUser;
        }

        try
        {
            // Reached only for a genuine SignalR-only circuit postback with no backing HttpContext —
            // the circuit's AuthenticationState is already established there (set during prerender/
            // circuit start), so this does no I/O and is safe to resolve synchronously.
            return authenticationStateProvider.GetAuthenticationStateAsync().GetAwaiter().GetResult().User;
        }
        catch
        {
            // sanctioned-silent: no HttpContext and no usable circuit state (e.g. DataSeeder's
            // background scope) — anonymous is the by-design fallback for a content-rating filter,
            // and this static helper predates any logger (see logging.md §"No Silent Catches").
            return new ClaimsPrincipal(new ClaimsIdentity());
        }
    }
}
