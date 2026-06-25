using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="NotificationCategoryVisuals"/> (WU33).
/// Verifies: all 9 categories return non-empty visual info; reused colors match source constants;
/// <c>AllCategories</c> returns all 9 in enum-value order.
/// </summary>
public class NotificationCategoryVisualsTests
{
    private static readonly NotificationCategoryEnum[] AllCats =
        Enum.GetValues<NotificationCategoryEnum>().OrderBy(c => (int)c).ToArray();

    // ── Each category returns non-empty fields ────────────────────────────────────

    [Theory]
    [MemberData(nameof(AllCatData))]
    public void For_AllCategories_ReturnsNonEmptyIconPath(NotificationCategoryEnum cat)
    {
        NotificationCategoryVisuals.For(cat).IconPath
            .Should().NotBeNullOrWhiteSpace($"{cat} must have a non-empty SVG path");
    }

    [Theory]
    [MemberData(nameof(AllCatData))]
    public void For_AllCategories_ReturnsNonEmptyAccentColor(NotificationCategoryEnum cat)
    {
        NotificationCategoryVisuals.For(cat).AccentColor
            .Should().NotBeNullOrWhiteSpace($"{cat} must have a non-empty accent color");
    }

    [Theory]
    [MemberData(nameof(AllCatData))]
    public void For_AllCategories_ReturnsNonEmptyLabel(NotificationCategoryEnum cat)
    {
        NotificationCategoryVisuals.For(cat).Label
            .Should().NotBeNullOrWhiteSpace($"{cat} must have a non-empty label");
    }

    // ── Specific color assertions (reuse discipline) ──────────────────────────────

    [Fact]
    public void For_YourFollows_AccentColorMatchesFollowTeal()
    {
        // YourFollows reuses the Follow interaction color — single source of truth.
        NotificationCategoryVisuals.For(NotificationCategoryEnum.YourFollows).AccentColor
            .Should().Be(UserStoryInteractionVisuals.For(UserStoryInteractionTypeEnum.Follow).AccentColor,
                "YourFollows reuses the Follow interaction accent (Manaphy Teal #2DBBA0)");
    }

    [Fact]
    public void For_Warnings_AccentColorMatchesIgnoreRed()
    {
        // Warnings reuses the Ignore interaction color — single source of truth.
        NotificationCategoryVisuals.For(NotificationCategoryEnum.Warnings).AccentColor
            .Should().Be(UserStoryInteractionVisuals.For(UserStoryInteractionTypeEnum.Ignore).AccentColor,
                "Warnings reuses the Ignore interaction accent (red #C04030)");
    }

    [Fact]
    public void For_YourRecommendations_AccentColorMatchesRecommendationGreen()
    {
        NotificationCategoryVisuals.For(NotificationCategoryEnum.YourRecommendations).AccentColor
            .Should().Be(RecommendationIcons.RecommendationAccentColor,
                "YourRecommendations reuses RecommendationIcons.RecommendationAccentColor (#5BB85A)");
    }

    // ── AllCategories ─────────────────────────────────────────────────────────────

    [Fact]
    public void AllCategories_Returns9Categories()
    {
        NotificationCategoryVisuals.AllCategories.Should().HaveCount(9,
            "there are 9 NotificationCategory values seeded (SiteNews=0 through YourReports=8)");
    }

    [Fact]
    public void AllCategories_StartsWithSiteNews()
    {
        NotificationCategoryVisuals.AllCategories.First().Should().Be(NotificationCategoryEnum.SiteNews,
            "SiteNews is enum value 0, first in display order");
    }

    [Fact]
    public void AllCategories_EndsWithYourReports()
    {
        NotificationCategoryVisuals.AllCategories.Last().Should().Be(NotificationCategoryEnum.YourReports,
            "YourReports is enum value 8, last in display order");
    }

    [Fact]
    public void AllCategories_ContainsAllNineValues()
    {
        NotificationCategoryVisuals.AllCategories
            .Should().BeEquivalentTo(AllCats,
                because: "AllCategories must enumerate all nine NotificationCategoryEnum values");
    }

    // Mutation sanity — verify the test can detect wrong mapping.
    [Fact]
    public void For_SiteNews_LabelIsSiteNews()
    {
        NotificationCategoryVisuals.For(NotificationCategoryEnum.SiteNews).Label
            .Should().Be("Site News");
    }

    public static IEnumerable<object[]> AllCatData() =>
        Enum.GetValues<NotificationCategoryEnum>().Select(c => new object[] { c });
}
