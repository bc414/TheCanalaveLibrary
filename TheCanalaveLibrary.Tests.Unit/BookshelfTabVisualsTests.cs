using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="BookshelfTabVisuals"/> and <see cref="BookshelfTabSlug"/> (WU27).
/// Verifies: all 11 tabs return non-empty visual info; Following tab is teal (#2DBBA0);
/// slug round-trip parse/format; AllTabs returns all 11 in display order.
/// </summary>
public class BookshelfTabVisualsTests
{
    private static readonly IReadOnlyList<BookshelfTab> AllTabs =
        Enum.GetValues<BookshelfTab>().OrderBy(t => (int)t).ToList();

    // ── Each tab returns non-empty fields ────────────────────────────────────────

    [Theory]
    [MemberData(nameof(AllTabData))]
    public void For_AllTabs_ReturnsNonEmptyIconPath(BookshelfTab tab)
    {
        BookshelfTabVisuals.For(tab).IconPath
            .Should().NotBeNullOrWhiteSpace($"{tab} must have a non-empty SVG path");
    }

    [Theory]
    [MemberData(nameof(AllTabData))]
    public void For_AllTabs_ReturnsNonEmptyAccentColor(BookshelfTab tab)
    {
        BookshelfTabVisuals.For(tab).AccentColor
            .Should().NotBeNullOrWhiteSpace($"{tab} must have a non-empty accent color");
    }

    [Theory]
    [MemberData(nameof(AllTabData))]
    public void For_AllTabs_ReturnsNonEmptyLabel(BookshelfTab tab)
    {
        BookshelfTabVisuals.For(tab).Label
            .Should().NotBeNullOrWhiteSpace($"{tab} must have a non-empty label");
    }

    [Theory]
    [MemberData(nameof(AllTabData))]
    public void For_AllTabs_ReturnsNonEmptySlug(BookshelfTab tab)
    {
        BookshelfTabVisuals.For(tab).Slug
            .Should().NotBeNullOrWhiteSpace($"{tab} must have a non-empty URL slug");
    }

    // ── Following reskin — must be Manaphy Teal ──────────────────────────────────

    [Fact]
    public void For_Following_AccentColorIsTeal()
    {
        BookshelfTabVisuals.For(BookshelfTab.Following).AccentColor
            .Should().Be("#2DBBA0",
                "Following was reskinned to Manaphy Teal (WU27) — green is reserved for curation tabs");
    }

    // ── Slug round-trip ──────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(AllTabData))]
    public void SlugRoundTrip_ForThenParse_ReturnsSameTab(BookshelfTab tab)
    {
        string slug = BookshelfTabSlug.For(tab);
        BookshelfTab? parsed = BookshelfTabSlug.Parse(slug);

        parsed.Should().Be(tab, $"Parse(For({tab})) must return the original tab");
    }

    [Fact]
    public void Parse_UnknownSlug_ReturnsNull()
    {
        BookshelfTabSlug.Parse("not-a-real-tab").Should().BeNull();
    }

    [Fact]
    public void Parse_NullSlug_ReturnsNull()
    {
        BookshelfTabSlug.Parse(null).Should().BeNull();
    }

    // ── AllTabs returns all 11 in display order ──────────────────────────────────

    [Fact]
    public void AllTabs_Returns11Tabs()
    {
        BookshelfTabVisuals.AllTabs.Should().HaveCount(11);
    }

    [Fact]
    public void AllTabs_StartsWithMyStories()
    {
        BookshelfTabVisuals.AllTabs.First().Should().Be(BookshelfTab.MyStories,
            "My Stories is position 0 in the display order");
    }

    [Fact]
    public void AllTabs_EndsWithIgnored()
    {
        BookshelfTabVisuals.AllTabs.Last().Should().Be(BookshelfTab.Ignored,
            "Ignored is the last tab in the display order");
    }

    // ── Distinct slugs and colors ────────────────────────────────────────────────

    [Fact]
    public void AllTabs_HaveDistinctSlugs()
    {
        IEnumerable<string> slugs = AllTabs.Select(t => BookshelfTabVisuals.For(t).Slug);
        slugs.Should().OnlyHaveUniqueItems("each tab has a distinct URL slug");
    }

    // Mutation sanity — verify the test can actually detect a wrong value.
    [Fact]
    public void MyStories_LabelIsMyStories_NotAnEmptyString()
    {
        BookshelfTabVisuals.For(BookshelfTab.MyStories).Label
            .Should().Be("My Stories");
    }

    public static IEnumerable<object[]> AllTabData() =>
        Enum.GetValues<BookshelfTab>().Select(t => new object[] { t });
}
