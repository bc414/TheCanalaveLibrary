using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="UserStoryInteractionVisuals"/> (WU16). Verifies that the locked
/// audit/UserStoryInteractions.md (2026-06-22) table is transcribed correctly:
/// all six entries return non-empty IconPath + AccentColor + Label; AccentColors match
/// the locked values; PrivateFavorite reuses Favorite's IconPath (color alone signals privacy).
/// </summary>
public class UserStoryInteractionVisualsTests
{
    [Theory]
    [InlineData(UserStoryInteractionTypeEnum.Favorite)]
    [InlineData(UserStoryInteractionTypeEnum.PrivateFavorite)]
    [InlineData(UserStoryInteractionTypeEnum.Follow)]
    [InlineData(UserStoryInteractionTypeEnum.Complete)]
    [InlineData(UserStoryInteractionTypeEnum.ReadLater)]
    [InlineData(UserStoryInteractionTypeEnum.Ignore)]
    public void For_AllTypes_ReturnsNonEmptyIconPath(UserStoryInteractionTypeEnum type)
    {
        UserStoryInteractionVisuals.Info info = UserStoryInteractionVisuals.For(type);
        info.IconPath.Should().NotBeNullOrWhiteSpace($"{type} must have a non-empty SVG path");
    }

    [Theory]
    [InlineData(UserStoryInteractionTypeEnum.Favorite)]
    [InlineData(UserStoryInteractionTypeEnum.PrivateFavorite)]
    [InlineData(UserStoryInteractionTypeEnum.Follow)]
    [InlineData(UserStoryInteractionTypeEnum.Complete)]
    [InlineData(UserStoryInteractionTypeEnum.ReadLater)]
    [InlineData(UserStoryInteractionTypeEnum.Ignore)]
    public void For_AllTypes_ReturnsNonEmptyAccentColor(UserStoryInteractionTypeEnum type)
    {
        UserStoryInteractionVisuals.Info info = UserStoryInteractionVisuals.For(type);
        info.AccentColor.Should().NotBeNullOrWhiteSpace($"{type} must have a non-empty accent color");
    }

    [Theory]
    [InlineData(UserStoryInteractionTypeEnum.Favorite)]
    [InlineData(UserStoryInteractionTypeEnum.PrivateFavorite)]
    [InlineData(UserStoryInteractionTypeEnum.Follow)]
    [InlineData(UserStoryInteractionTypeEnum.Complete)]
    [InlineData(UserStoryInteractionTypeEnum.ReadLater)]
    [InlineData(UserStoryInteractionTypeEnum.Ignore)]
    public void For_AllTypes_ReturnsNonEmptyLabel(UserStoryInteractionTypeEnum type)
    {
        UserStoryInteractionVisuals.Info info = UserStoryInteractionVisuals.For(type);
        info.Label.Should().NotBeNullOrWhiteSpace($"{type} must have a non-empty label");
    }

    // ── Locked accent colors (verbatim from audit table 2026-06-22) ──────────────

    [Theory]
    [InlineData(UserStoryInteractionTypeEnum.Favorite, "#E8507A")]
    [InlineData(UserStoryInteractionTypeEnum.PrivateFavorite, "#C040A8")]
    [InlineData(UserStoryInteractionTypeEnum.Follow, "#2DBBA0")]
    [InlineData(UserStoryInteractionTypeEnum.Complete, "#E8B84B")]
    [InlineData(UserStoryInteractionTypeEnum.ReadLater, "#2E6FBF")]
    [InlineData(UserStoryInteractionTypeEnum.Ignore, "#C04030")]
    public void For_LockedAccentColors_MatchAuditTable(UserStoryInteractionTypeEnum type, string expectedColor)
    {
        UserStoryInteractionVisuals.For(type).AccentColor.Should().Be(expectedColor,
            "AccentColors are locked in audit/UserStoryInteractions.md (2026-06-22)");
    }

    // ── PrivateFavorite reuses Favorite's IconPath (color alone signals privacy) ─

    [Fact]
    public void PrivateFavorite_ReusesTheSameIconPath_As_Favorite()
    {
        string favPath = UserStoryInteractionVisuals.For(UserStoryInteractionTypeEnum.Favorite).IconPath;
        string privPath = UserStoryInteractionVisuals.For(UserStoryInteractionTypeEnum.PrivateFavorite).IconPath;

        privPath.Should().Be(favPath,
            "PrivateFavorite uses the same filled-heart shape as Favorite; color alone signals privacy");
    }

    // ── Distinct accent colors (no two types share a color) ─────────────────────

    [Fact]
    public void AllSixTypes_HaveDistinctAccentColors()
    {
        IEnumerable<string> colors = Enum.GetValues<UserStoryInteractionTypeEnum>()
            .Select(t => UserStoryInteractionVisuals.For(t).AccentColor);

        colors.Should().OnlyHaveUniqueItems("each interaction type has a distinct color in the palette");
    }
}
