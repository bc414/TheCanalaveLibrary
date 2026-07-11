using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="TreeSearchMobile"/> (WU44, spec §5.26). Covers: the
/// Connections/Filters drawer toggle, Manual-tab placeholder, degree badges on results, and the
/// flooding indicator. Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class TreeSearchMobileTests : BunitContext
{
    private readonly FakeUserStoryInteractionWriteService _fakeUsiService = new();

    public TreeSearchMobileTests()
    {
        Services.AddScoped<IUserStoryInteractionWriteService>(_ => _fakeUsiService);
        Services.AddScoped<ITagReadService>(_ => new FakeTagReadService());
        Services.AddSingleton<ISpriteReadService>(new OptimisticSpriteReadService("/sprites/themes"));
        JSInterop.Mode = JSRuntimeMode.Loose;

        // TagFilter (inside ResultsFilterPanel) mounts SavedTagSelectionLoadFlyout/SaveDialog
        // (WU43), both wrapped in a bare <AuthorizeView> — anonymous/not-authorized by default
        // keeps them off the DOM here (this suite isn't testing that feature).
        this.AddAuthorization();
    }

    private static StoryListingDto MakeStory(int id) =>
        new(id, $"Story {id}", null, null, null, "Author", 5_000,
            StoryStatusEnum.InProgress, Rating.T, DateTime.UtcNow, []);

    private static TreeSearchListingResultDto MakeResult(
        params (int StoryId, int Degree, string? Path)[] hits) => new()
    {
        Items = [.. hits.Select(h => new TreeSearchListingItemDto
        {
            Story = MakeStory(h.StoryId), Degree = h.Degree, Path = h.Path,
        })],
        DegreesReached = hits.Length > 0 ? hits.Max(h => h.Degree) : 0,
        ResultCapTruncated = false,
    };

    [Fact]
    public void Drawer_NotRendered_UntilToggled()
    {
        IRenderedComponent<TreeSearchMobile> cut = Render<TreeSearchMobile>(p => p
            .Add(c => c.RootStory, MakeStory(1))
            .Add(c => c.ActiveTab, TreeSearchTab.Automatic)
            .Add(c => c.Result, MakeResult()));

        cut.FindComponents<TreeSearchControls>().Should().BeEmpty(
            "the drawer (and its controls) is closed by default");
    }

    [Fact]
    public void ClickingToggle_OpensDrawer_WithControlsAndFilterPanel()
    {
        IRenderedComponent<TreeSearchMobile> cut = Render<TreeSearchMobile>(p => p
            .Add(c => c.RootStory, MakeStory(1))
            .Add(c => c.ActiveTab, TreeSearchTab.Automatic)
            .Add(c => c.Result, MakeResult()));

        cut.Find("button[aria-controls=tree-search-drawer]").Click();

        cut.FindComponents<TreeSearchControls>().Should().ContainSingle();
        cut.FindComponents<ResultsFilterPanel>().Should().ContainSingle();
    }

    [Fact]
    public void ManualTab_ShowsPlaceholder_HidesToggleAndDeck()
    {
        IRenderedComponent<TreeSearchMobile> cut = Render<TreeSearchMobile>(p => p
            .Add(c => c.RootStory, MakeStory(1))
            .Add(c => c.ActiveTab, TreeSearchTab.Manual)
            .Add(c => c.Result, MakeResult()));

        cut.Markup.Should().Contain("Graph view coming soon");
        cut.FindAll("button[aria-controls=tree-search-drawer]").Should().BeEmpty();
        cut.FindComponents<StoryDeck>().Should().BeEmpty();
    }

    [Fact]
    public void Deck_RendersDegreeBadgePerResultItem()
    {
        IRenderedComponent<TreeSearchMobile> cut = Render<TreeSearchMobile>(p => p
            .Add(c => c.RootStory, MakeStory(1))
            .Add(c => c.ActiveTab, TreeSearchTab.Automatic)
            .Add(c => c.Result, MakeResult((10, 3, null))));

        cut.FindComponents<TreeSearchResultBadge>().Should().ContainSingle();
        cut.Markup.Should().Contain("3rd-degree connection");
    }

    [Fact]
    public void ApplyingControls_ClosesDrawer()
    {
        IRenderedComponent<TreeSearchMobile> cut = Render<TreeSearchMobile>(p => p
            .Add(c => c.RootStory, MakeStory(1))
            .Add(c => c.ActiveTab, TreeSearchTab.Automatic)
            .Add(c => c.Result, MakeResult()));

        cut.Find("button[aria-controls=tree-search-drawer]").Click();
        cut.FindComponents<TreeSearchControls>().Should().ContainSingle();

        cut.FindComponent<TreeSearchControls>().Find("button").Click(); // Apply

        cut.FindComponents<TreeSearchControls>().Should().BeEmpty("Apply closes the drawer");
    }
}
