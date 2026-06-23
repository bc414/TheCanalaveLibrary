using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="InteractionVisuals"/> (WU16). Verifies that the locked
/// audit/UserStoryInteractions.md (2026-06-22) table is transcribed correctly:
/// all six entries return non-empty IconPath + AccentColor + Label; AccentColors match
/// the locked values; PrivateFavorite reuses Favorite's IconPath (color alone signals privacy).
/// </summary>
public class InteractionVisualsTests
{
    [Theory]
    [InlineData(InteractionTypeEnum.Favorite)]
    [InlineData(InteractionTypeEnum.PrivateFavorite)]
    [InlineData(InteractionTypeEnum.Follow)]
    [InlineData(InteractionTypeEnum.Complete)]
    [InlineData(InteractionTypeEnum.ReadLater)]
    [InlineData(InteractionTypeEnum.Ignore)]
    public void For_AllTypes_ReturnsNonEmptyIconPath(InteractionTypeEnum type)
    {
        InteractionVisuals.Info info = InteractionVisuals.For(type);
        info.IconPath.Should().NotBeNullOrWhiteSpace($"{type} must have a non-empty SVG path");
    }

    [Theory]
    [InlineData(InteractionTypeEnum.Favorite)]
    [InlineData(InteractionTypeEnum.PrivateFavorite)]
    [InlineData(InteractionTypeEnum.Follow)]
    [InlineData(InteractionTypeEnum.Complete)]
    [InlineData(InteractionTypeEnum.ReadLater)]
    [InlineData(InteractionTypeEnum.Ignore)]
    public void For_AllTypes_ReturnsNonEmptyAccentColor(InteractionTypeEnum type)
    {
        InteractionVisuals.Info info = InteractionVisuals.For(type);
        info.AccentColor.Should().NotBeNullOrWhiteSpace($"{type} must have a non-empty accent color");
    }

    [Theory]
    [InlineData(InteractionTypeEnum.Favorite)]
    [InlineData(InteractionTypeEnum.PrivateFavorite)]
    [InlineData(InteractionTypeEnum.Follow)]
    [InlineData(InteractionTypeEnum.Complete)]
    [InlineData(InteractionTypeEnum.ReadLater)]
    [InlineData(InteractionTypeEnum.Ignore)]
    public void For_AllTypes_ReturnsNonEmptyLabel(InteractionTypeEnum type)
    {
        InteractionVisuals.Info info = InteractionVisuals.For(type);
        info.Label.Should().NotBeNullOrWhiteSpace($"{type} must have a non-empty label");
    }

    // ── Locked accent colors (verbatim from audit table 2026-06-22) ──────────────

    [Theory]
    [InlineData(InteractionTypeEnum.Favorite, "#E8507A")]
    [InlineData(InteractionTypeEnum.PrivateFavorite, "#C040A8")]
    [InlineData(InteractionTypeEnum.Follow, "#4A9B52")]
    [InlineData(InteractionTypeEnum.Complete, "#E8B84B")]
    [InlineData(InteractionTypeEnum.ReadLater, "#2E6FBF")]
    [InlineData(InteractionTypeEnum.Ignore, "#C04030")]
    public void For_LockedAccentColors_MatchAuditTable(InteractionTypeEnum type, string expectedColor)
    {
        InteractionVisuals.For(type).AccentColor.Should().Be(expectedColor,
            "AccentColors are locked in audit/UserStoryInteractions.md (2026-06-22)");
    }

    // ── PrivateFavorite reuses Favorite's IconPath (color alone signals privacy) ─

    [Fact]
    public void PrivateFavorite_ReusesTheSameIconPath_As_Favorite()
    {
        string favPath = InteractionVisuals.For(InteractionTypeEnum.Favorite).IconPath;
        string privPath = InteractionVisuals.For(InteractionTypeEnum.PrivateFavorite).IconPath;

        privPath.Should().Be(favPath,
            "PrivateFavorite uses the same filled-heart shape as Favorite; color alone signals privacy");
    }

    // ── Distinct accent colors (no two types share a color) ─────────────────────

    [Fact]
    public void AllSixTypes_HaveDistinctAccentColors()
    {
        IEnumerable<string> colors = Enum.GetValues<InteractionTypeEnum>()
            .Select(t => InteractionVisuals.For(t).AccentColor);

        colors.Should().OnlyHaveUniqueItems("each interaction type has a distinct color in the palette");
    }
}
