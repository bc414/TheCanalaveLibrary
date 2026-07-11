namespace TheCanalaveLibrary.Core;

/// <summary>
/// Scoped, read-side "who is the current viewer" companion to the <see cref="User"/> entity — minted
/// WU12. Holds only hot scalar fields needed to shape queries (the content-rating filter, sprite
/// resolution), never the full entity — that would defeat the hot/cold partition design and the DTO
/// Firewall. See layer2-services.md / cross-cutting.md "Active-User Context" for the full rationale and
/// for what's deliberately excluded (display name/avatar, ReaderDisplaySettings, notification prefs).
/// </summary>
public interface IActiveUserContext
{
    /// <summary>Null when anonymous.</summary>
    int? UserId { get; }
    bool IsAuthenticated { get; }

    /// <summary>Feeds the content-rating query filter on <see cref="Story"/> (cross-cutting.md).</summary>
    bool ShowMatureContent { get; }

    /// <summary>
    /// URL-safe theme slug (e.g. <c>"pokemon"</c>). Feeds <c>ThemeContext.Slug</c>, which is cascaded
    /// from <c>ThemeContextProvider</c> in <c>Routes.razor</c> and consumed by sprite render components.
    /// Never the display name (e.g. <c>"Pokémon"</c>) — the slug is a direct sprite URL path segment.
    /// </summary>
    string Theme { get; }
    bool PrefersAnimatedSprites { get; }

    /// <summary>
    /// Valid for **server-side authorization** in write services (see <c>cross-cutting.md</c>
    /// "Active-User Context" table) and for deciding when a read path calls
    /// <c>IgnoreQueryFilters</c>. UI role gating uses <c>&lt;AuthorizeView Roles="…"&gt;</c>
    /// instead (SharedUI never injects this interface). <c>IsInRole</c> is literal — Admin does NOT
    /// inherit Moderator; list both roles wherever either should be accepted.
    /// </summary>
    bool IsModerator { get; }
    bool IsAdmin { get; }
}

/// <summary>
/// Claim type names this app bakes into the auth cookie at sign-in (see
/// <c>ApplicationUserClaimsPrincipalFactory</c> in Server/Identity/) so <see cref="IActiveUserContext"/>
/// can be populated from claims alone, with zero DbContext dependency — that's what breaks what would
/// otherwise be a circular dependency with <c>ApplicationDbContext</c>'s content-rating query filter.
/// Shared between the factory that writes these claims and <c>ServerActiveUserContext</c>, which reads
/// them — both must agree on the literal strings, hence one source of truth here.
/// </summary>
public static class ActiveUserClaimTypes
{
    public const string ShowMatureContent = "canalave:show_mature_content";
    public const string Theme = "canalave:theme";
    public const string PrefersAnimatedSprites = "canalave:prefers_animated_sprites";

    /// <summary>
    /// <see cref="AccountStatusEnum"/>'s name (e.g. <c>"Warned"</c>), baked at sign-in — WU38a.
    /// Not part of <see cref="IActiveUserContext"/> (that interface is scoped to query-shaping
    /// fields only): this claim exists purely for <c>AccountStatusBanner</c> to read from cascaded
    /// <c>AuthenticationState</c>, no service injection. Same staleness caveat as the other baked
    /// claims — stale until next sign-in unless the write path calls
    /// <c>SignInManager.RefreshSignInAsync</c>; a freshly-Warned user sees the banner starting at
    /// their next sign-in, with the WU34 notification as the immediate channel meanwhile.
    /// </summary>
    public const string AccountStatus = "canalave:account_status";
}
