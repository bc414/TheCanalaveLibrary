using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Settable stand-in for <c>ServerActiveUserContext</c>, registered in <see cref="TestAppFactory"/> in
/// place of the real claims-based implementation — see testing.md "Driving the content-rating filter."
/// Lets a test flip <see cref="ShowMatureContent"/>/<see cref="UserId"/>/role flags directly, with no
/// real sign-in flow, cookies, or <c>SecurityStampValidator</c> involved.
/// </summary>
public class FakeActiveUserContext : IActiveUserContext
{
    public int? UserId { get; set; }
    public bool IsAuthenticated { get; set; }
    public bool ShowMatureContent { get; set; }
    public string Theme { get; set; } = "pokemon";
    public bool PrefersAnimatedSprites { get; set; } = true;
    public bool IsModerator { get; set; }
    public bool IsAdmin { get; set; }

    // ── Viewer consent state (WU-AccessGate) ──
    // Stand-ins for the prefs-cookie reveals (anonymous) and the VerifiedBotMiddleware signal.
    // Authenticated reveals are DB rows — seed user_content_reveals directly instead.
    public bool IsVerifiedBot { get; set; }
    public HashSet<(RevealedEntityType EntityType, int EntityId)> AnonReveals { get; } = [];

    public bool HasAnonRevealed(RevealedEntityType entityType, int entityId) =>
        !IsAuthenticated && AnonReveals.Contains((entityType, entityId));

    public static FakeActiveUserContext Anonymous() => new();

    public static FakeActiveUserContext AuthenticatedUser(int userId, bool showMatureContent) => new()
    {
        UserId = userId,
        IsAuthenticated = true,
        ShowMatureContent = showMatureContent
    };

    public static FakeActiveUserContext Moderator(int userId) => new()
    {
        UserId = userId,
        IsAuthenticated = true,
        ShowMatureContent = false,
        IsModerator = true
    };
}
