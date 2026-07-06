using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="StoryViewStats"/> (Feature 45 — the on-demand, non-sortable
/// view-count reveal in StoryCard's caret dropdown). Covers: nothing fetched until the user asks
/// (the "View stats" button renders, no count); clicking reveals the formatted total; singular/
/// plural label; reset when the component slot is reused for a different story.
/// </summary>
public class StoryViewStatsTests : BunitContext
{
    private readonly FakeStoryReadService _storyReadService = new();

    public StoryViewStatsTests()
    {
        Services.AddSingleton<IStoryReadService>(_storyReadService);
    }

    [Fact]
    public void BeforeReveal_ShowsButton_AndNoCount()
    {
        _storyReadService.TotalViews = 1234;

        IRenderedComponent<StoryViewStats> cut = Render<StoryViewStats>(p => p.Add(c => c.StoryId, 1));

        cut.Markup.Should().Contain("View stats");
        cut.Markup.Should().NotContain("1,234", "the SUM is fetched only when the user asks");
    }

    [Fact]
    public void ClickingReveal_ShowsFormattedTotal()
    {
        _storyReadService.TotalViews = 1234;

        IRenderedComponent<StoryViewStats> cut = Render<StoryViewStats>(p => p.Add(c => c.StoryId, 1));
        cut.Find("button").Click();

        cut.Markup.Should().Contain("1,234 views");
        cut.FindAll("button").Should().BeEmpty("the reveal replaces the button with the count");
    }

    [Fact]
    public void SingleView_UsesSingularLabel()
    {
        _storyReadService.TotalViews = 1;

        IRenderedComponent<StoryViewStats> cut = Render<StoryViewStats>(p => p.Add(c => c.StoryId, 1));
        cut.Find("button").Click();

        cut.Markup.Should().Contain("1 view");
        cut.Markup.Should().NotContain("1 views");
    }

    [Fact]
    public void StoryIdChange_ResetsTheReveal()
    {
        _storyReadService.TotalViews = 50;

        IRenderedComponent<StoryViewStats> cut = Render<StoryViewStats>(p => p.Add(c => c.StoryId, 1));
        cut.Find("button").Click();
        cut.Markup.Should().Contain("50 views");

        // The deck pages; this component slot now represents a different story.
        cut.Render(p => p.Add(c => c.StoryId, 2));

        cut.Markup.Should().Contain("View stats", "a new story must not inherit the old reveal");
        cut.Markup.Should().NotContain("50 views");
    }
}
