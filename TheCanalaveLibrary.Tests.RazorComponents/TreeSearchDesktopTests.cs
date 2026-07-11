using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="TreeSearchDesktop"/> (WU44, spec §5.26). Covers: root-entity header
/// (story vs. user), tab strip + Manual-tab placeholder, Automatic tab composes
/// <see cref="TreeSearchControls"/> + <see cref="ResultsFilterPanel"/> + <see cref="StoryDeck"/>
/// with a degree badge per card, the flooding indicator, and the Apply callbacks.
///
/// Not tested: Tailwind layout, live traversal (Integration tier — <c>TreeSearchComposeTests</c>),
/// L4 visual sign-off (human, Stage 6). Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class TreeSearchDesktopTests : BunitContext
{
    private readonly FakeUserStoryInteractionWriteService _fakeUsiService = new();

    public TreeSearchDesktopTests()
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

    private static UserCardDto MakeUser(int id = 1) => new(id, $"User{id}", null, null, []);

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

    // ── Root-entity header ────────────────────────────────────────────────────────

    [Fact]
    public void StoryRoot_RendersStoryCardHeader()
    {
        IRenderedComponent<TreeSearchDesktop> cut = Render<TreeSearchDesktop>(p => p
            .Add(c => c.RootStory, MakeStory(1))
            .Add(c => c.ActiveTab, TreeSearchTab.Automatic)
            .Add(c => c.Result, MakeResult()));

        cut.FindComponents<StoryCard>().Should().ContainSingle();
        cut.FindComponents<UserCard>().Should().BeEmpty();
    }

    [Fact]
    public void UserRoot_RendersUserCardHeader()
    {
        IRenderedComponent<TreeSearchDesktop> cut = Render<TreeSearchDesktop>(p => p
            .Add(c => c.RootUser, MakeUser())
            .Add(c => c.ActiveTab, TreeSearchTab.Automatic)
            .Add(c => c.Result, MakeResult()));

        cut.FindComponents<UserCard>().Should().ContainSingle();
        cut.FindComponents<StoryCard>().Should().BeEmpty();
    }

    // ── Tab behavior ───────────────────────────────────────────────────────────────

    [Fact]
    public void ManualTab_ShowsPlaceholder_HidesControlsAndDeck()
    {
        IRenderedComponent<TreeSearchDesktop> cut = Render<TreeSearchDesktop>(p => p
            .Add(c => c.RootStory, MakeStory(1))
            .Add(c => c.ActiveTab, TreeSearchTab.Manual)
            .Add(c => c.Result, MakeResult()));

        cut.Markup.Should().Contain("Graph view coming soon");
        cut.FindComponents<TreeSearchControls>().Should().BeEmpty();
        cut.FindComponents<StoryDeck>().Should().BeEmpty();
    }

    [Fact]
    public void AutomaticTab_RendersControlsFilterPanelAndDeck()
    {
        IRenderedComponent<TreeSearchDesktop> cut = Render<TreeSearchDesktop>(p => p
            .Add(c => c.RootStory, MakeStory(1))
            .Add(c => c.ActiveTab, TreeSearchTab.Automatic)
            .Add(c => c.Result, MakeResult((2, 2, null))));

        cut.FindComponents<TreeSearchControls>().Should().ContainSingle();
        cut.FindComponents<ResultsFilterPanel>().Should().ContainSingle();
        cut.FindComponents<StoryDeck>().Should().ContainSingle();
    }

    // ── Results + degree badge ──────────────────────────────────────────────────────

    [Fact]
    public void Deck_RendersOneCardPerResultItem_WithDegreeBadge()
    {
        IRenderedComponent<TreeSearchDesktop> cut = Render<TreeSearchDesktop>(p => p
            .Add(c => c.RootStory, MakeStory(1))
            .Add(c => c.ActiveTab, TreeSearchTab.Automatic)
            .Add(c => c.Result, MakeResult((10, 2, null), (11, 4, null))));

        // Root header (StoryId 1) + two result cards = 3 StoryCards total.
        cut.FindComponents<StoryCard>().Should().HaveCount(3);
        cut.FindComponents<TreeSearchResultBadge>().Should().HaveCount(2,
            "one degree badge per result item, none for the root header");
        cut.Markup.Should().Contain("2nd-degree connection").And.Contain("4th-degree connection");
    }

    [Fact]
    public void ResultCapTruncated_ShowsFloodingIndicator()
    {
        TreeSearchListingResultDto result = MakeResult((10, 2, null)) with { ResultCapTruncated = true };
        IRenderedComponent<TreeSearchDesktop> cut = Render<TreeSearchDesktop>(p => p
            .Add(c => c.RootStory, MakeStory(1))
            .Add(c => c.ActiveTab, TreeSearchTab.Automatic)
            .Add(c => c.Result, result));

        cut.Markup.Should().Contain("Showing a sample of many connections");
    }

    [Fact]
    public void NotTruncated_HidesFloodingIndicator()
    {
        IRenderedComponent<TreeSearchDesktop> cut = Render<TreeSearchDesktop>(p => p
            .Add(c => c.RootStory, MakeStory(1))
            .Add(c => c.ActiveTab, TreeSearchTab.Automatic)
            .Add(c => c.Result, MakeResult((10, 2, null))));

        cut.Markup.Should().NotContain("Showing a sample of many connections");
    }

    // ── Callbacks ────────────────────────────────────────────────────────────────────

    [Fact]
    public void OnTabChanged_Fires_WhenManualTabClicked()
    {
        TreeSearchTab? emitted = null;
        IRenderedComponent<TreeSearchDesktop> cut = Render<TreeSearchDesktop>(p => p
            .Add(c => c.RootStory, MakeStory(1))
            .Add(c => c.ActiveTab, TreeSearchTab.Automatic)
            .Add(c => c.Result, MakeResult())
            .Add(c => c.OnTabChanged, EventCallback.Factory.Create<TreeSearchTab>(this, t => emitted = t)));

        cut.FindAll("button[role=tab]")[1].Click();

        emitted.Should().Be(TreeSearchTab.Manual);
    }

    [Fact]
    public void OnControlsApply_Fires_WhenControlsApplyClicked()
    {
        bool fired = false;
        IRenderedComponent<TreeSearchDesktop> cut = Render<TreeSearchDesktop>(p => p
            .Add(c => c.RootStory, MakeStory(1))
            .Add(c => c.ActiveTab, TreeSearchTab.Automatic)
            .Add(c => c.Result, MakeResult())
            .Add(c => c.OnControlsApply, EventCallback.Factory.Create<TreeSearchControlsSelection>(this, _ => fired = true)));

        cut.FindComponent<TreeSearchControls>().Find("button").Click();

        fired.Should().BeTrue();
    }

    // Mutation sanity: switching tabs changes which components render.
    [Fact]
    public void MutationSanity_SwitchingToManual_RemovesDeck()
    {
        IRenderedComponent<TreeSearchDesktop> cut = Render<TreeSearchDesktop>(p => p
            .Add(c => c.RootStory, MakeStory(1))
            .Add(c => c.ActiveTab, TreeSearchTab.Automatic)
            .Add(c => c.Result, MakeResult((10, 2, null))));

        cut.FindComponents<StoryDeck>().Should().ContainSingle();

        cut.Render(p => p.Add(c => c.ActiveTab, TreeSearchTab.Manual));

        cut.FindComponents<StoryDeck>().Should().BeEmpty();
    }
}
