using Bunit;
using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="SeriesMembershipBox"/> (WU41) — the "Part of series: X — Part N of
/// M" box with Previous/Next in-series navigation on the story page. No @inject — data arrives
/// fully computed via <see cref="StorySeriesMembershipDto"/> (the component trusts the read
/// service's viewer-visible Position/Count/Prev/Next; it does no filtering of its own).
/// Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class SeriesMembershipBoxTests : BunitContext
{
    private static StorySeriesMembershipDto MakeMembership(
        int seriesId = 1,
        string seriesName = "The Kanto Chronicles",
        int position = 2,
        int count = 3,
        int? prevStoryId = 10,
        string? prevStoryTitle = "Part One",
        int? nextStoryId = 12,
        string? nextStoryTitle = "Part Three") =>
        new(seriesId, seriesName, position, count, prevStoryId, prevStoryTitle, nextStoryId, nextStoryTitle);

    [Fact]
    public void SeriesMembershipBox_RendersSeriesNameAsLink()
    {
        IRenderedComponent<SeriesMembershipBox> cut = Render<SeriesMembershipBox>(p => p
            .Add(c => c.Membership, MakeMembership(seriesId: 5, seriesName: "The Kanto Chronicles")));

        cut.Markup.Should().Contain("The Kanto Chronicles");
        cut.FindAll("a").Should().Contain(a => a.GetAttribute("href")!.StartsWith("/series/5/"));
    }

    [Fact]
    public void SeriesMembershipBox_RendersPositionAndCount()
    {
        IRenderedComponent<SeriesMembershipBox> cut = Render<SeriesMembershipBox>(p => p
            .Add(c => c.Membership, MakeMembership(position: 2, count: 3)));

        cut.Markup.Should().Contain("Part 2 of 3");
    }

    [Fact]
    public void SeriesMembershipBox_PrevPresent_RendersPrevLink()
    {
        IRenderedComponent<SeriesMembershipBox> cut = Render<SeriesMembershipBox>(p => p
            .Add(c => c.Membership, MakeMembership(prevStoryId: 10, prevStoryTitle: "Part One")));

        cut.Markup.Should().Contain("Part One");
        cut.FindAll("a").Should().Contain(a => a.GetAttribute("href") == "/story/10");
    }

    [Fact]
    public void SeriesMembershipBox_PrevNull_RendersDisabledPrevious_NoLink()
    {
        IRenderedComponent<SeriesMembershipBox> cut = Render<SeriesMembershipBox>(p => p
            .Add(c => c.Membership, MakeMembership(prevStoryId: null, prevStoryTitle: null)));

        // Only the series-name link and (still-present) Next link should exist — no /story/ link
        // for a null Prev.
        cut.FindAll("a").Should().NotContain(a => a.GetAttribute("href") == "/story/");
        cut.Markup.Should().Contain("aria-disabled");
    }

    [Fact]
    public void SeriesMembershipBox_NextPresent_RendersNextLink()
    {
        IRenderedComponent<SeriesMembershipBox> cut = Render<SeriesMembershipBox>(p => p
            .Add(c => c.Membership, MakeMembership(nextStoryId: 12, nextStoryTitle: "Part Three")));

        cut.Markup.Should().Contain("Part Three");
        cut.FindAll("a").Should().Contain(a => a.GetAttribute("href") == "/story/12");
    }

    [Fact]
    public void SeriesMembershipBox_NextNull_RendersDisabledNext()
    {
        IRenderedComponent<SeriesMembershipBox> cut = Render<SeriesMembershipBox>(p => p
            .Add(c => c.Membership, MakeMembership(nextStoryId: null, nextStoryTitle: null)));

        cut.Markup.Should().Contain("Next");
        cut.FindAll("a").Should().NotContain(a => a.GetAttribute("href") == "/story/");
    }

    [Fact]
    public void SeriesMembershipBox_FirstAndLast_BothDisabled()
    {
        IRenderedComponent<SeriesMembershipBox> cut = Render<SeriesMembershipBox>(p => p
            .Add(c => c.Membership, MakeMembership(
                position: 1, count: 1, prevStoryId: null, prevStoryTitle: null,
                nextStoryId: null, nextStoryTitle: null)));

        // Only the series-name link remains.
        cut.FindAll("a").Should().HaveCount(1);
        cut.Markup.Should().Contain("Part 1 of 1");
    }
}
