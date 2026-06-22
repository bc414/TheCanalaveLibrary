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
    public string Theme { get; set; } = "Pokémon";
    public bool PrefersAnimatedSprites { get; set; } = true;
    public bool IsModerator { get; set; }
    public bool IsAdmin { get; set; }

    public static FakeActiveUserContext Anonymous() => new();

    public static FakeActiveUserContext AuthenticatedUser(int userId, bool showMatureContent) => new()
    {
        UserId = userId,
        IsAuthenticated = true,
        ShowMatureContent = showMatureContent
    };
}
