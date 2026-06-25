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

    /// <summary>Feeds <c>ISpriteReadService.GetSpriteUrl(theme, ...)</c>.</summary>
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
}
