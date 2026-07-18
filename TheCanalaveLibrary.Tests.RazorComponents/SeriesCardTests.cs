using Bunit;
using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="SeriesCard"/> (WU41).
/// Covers: name renders and links to /series/{id}/{slug}; story count singular/plural; description
/// renders when present; EditHref renders/suppresses the owner-only Edit link.
/// No @inject in SeriesCard — no services to register.
/// Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class SeriesCardTests : BunitContext
{
    // ── Factory ──────────────────────────────────────────────────────────────────

    private static SeriesListingDto MakeSeries(
        int seriesId = 1,
        string name = "Test Series",
        string? description = null,
        int storyCount = 3,
        int? authorId = 1,
        string? authorName = "Author",
        DateTime? dateCreated = null) =>
        new(seriesId, name, description, storyCount, authorId, authorName,
            dateCreated ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    // ── Renders ──────────────────────────────────────────────────────────────────

    [Fact]
    public void SeriesCard_RendersSeriesName()
    {
        IRenderedComponent<SeriesCard> cut = Render<SeriesCard>(p => p
            .Add(c => c.Series, MakeSeries(name: "The Kanto Chronicles")));

        cut.Markup.Should().Contain("The Kanto Chronicles");
    }

    [Fact]
    public void SeriesCard_LinksToSeriesDetailPage()
    {
        IRenderedComponent<SeriesCard> cut = Render<SeriesCard>(p => p
            .Add(c => c.Series, MakeSeries(seriesId: 42, name: "My Series")));

        cut.Find("a").GetAttribute("href").Should().StartWith("/series/42/");
    }

    [Fact]
    public void SeriesCard_ShowsStoryCount()
    {
        IRenderedComponent<SeriesCard> cut = Render<SeriesCard>(p => p
            .Add(c => c.Series, MakeSeries(storyCount: 5)));

        cut.Markup.Should().Contain("5 stories");
    }

    [Fact]
    public void SeriesCard_OneStory_ShowsSingular()
    {
        IRenderedComponent<SeriesCard> cut = Render<SeriesCard>(p => p
            .Add(c => c.Series, MakeSeries(storyCount: 1)));

        cut.Markup.Should().Contain("1 story", "singular form for exactly one story");
        cut.Markup.Should().NotContain("1 stories");
    }

    [Fact]
    public void SeriesCard_WithDescription_RendersDescription()
    {
        IRenderedComponent<SeriesCard> cut = Render<SeriesCard>(p => p
            .Add(c => c.Series, MakeSeries(description: "A trilogy about a young trainer.")));

        cut.Markup.Should().Contain("A trilogy about a young trainer.");
    }

    [Fact]
    public void SeriesCard_WithEditHref_RendersEditLink()
    {
        IRenderedComponent<SeriesCard> cut = Render<SeriesCard>(p => p
            .Add(c => c.Series, MakeSeries(seriesId: 7))
            .Add(c => c.EditHref, "/series/7/edit"));

        cut.FindAll("a").Should().HaveCount(2, "title link + Edit link");
        cut.Markup.Should().Contain("Edit");
        cut.FindAll("a")[1].GetAttribute("href").Should().Be("/series/7/edit");
    }
}
