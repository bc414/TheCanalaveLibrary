using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="TreeSearchPage"/> (WU44, spec §5.26; retargeted from the former
/// TreeSearchDesktop composite 2026-07-18, WU-ResponsiveMerge — the page now owns its markup and
/// resolves root + traversal itself). Covers: root-entity header (story vs. user), tab strip
/// (Explore / Deep Dive composites swap in, Automatic controls + deck swap out), Automatic tab
/// composes <see cref="TreeSearchControls"/> + <see cref="ResultsFilterPanel"/> +
/// <see cref="StoryDeck"/> with a degree badge per card, the flooding indicator, and the
/// controls-Apply re-search path.
///
/// Not tested: Tailwind layout, live traversal (Integration tier — <c>TreeSearchComposeTests</c>),
/// L4 visual sign-off (human, Stage 6). Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class TreeSearchPageTests : BunitContext
{
    private readonly FakeUserStoryInteractionWriteService _fakeUsiService = new();
    private readonly FakeRelatedStoriesStoryReadService _storyReadService = new();
    private readonly FakeTreeSearchReadService _treeSearchService = new();

    public TreeSearchPageTests()
    {
        // Page injections: root story listing (IStoryReadService), root user header
        // (IUserProfileReadService), §8.7 defaults, per-viewer states, and the traversal service.
        Services.AddScoped<IStoryReadService>(_ => _storyReadService);
        Services.AddScoped<IUserProfileReadService>(_ => new FakeUserProfileReadService(MakeHeader()));
        Services.AddScoped<IUserStoryInteractionReadService>(_ => new FakeInteractionReadService());
        Services.AddScoped<IDiscoveryDefaultsReadService>(_ => new FakeDiscoveryDefaultsReadService());
        Services.AddScoped<ITreeSearchReadService>(_ => _treeSearchService);
        Services.AddScoped<IUserStoryInteractionWriteService>(_ => _fakeUsiService);
        Services.AddScoped<ITagReadService>(_ => new FakeTagReadService());
        Services.AddSingleton<ISpriteReadService>(new OptimisticSpriteReadService("/sprites/themes"));
        // WU40 tabs (Explore/DeepDive) are self-contained composites that own their reads.
        Services.AddScoped<IManualTreeSearchReadService>(_ => new FakeManualTreeSearchReadService());
        Services.AddScoped<ManualTreeStore>();
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Supplies the Task<AuthenticationState> cascade the page awaits (anonymous is fine).
        // TagFilter (inside ResultsFilterPanel) mounts WU43 flyouts behind a bare <AuthorizeView> —
        // anonymous keeps them off the DOM here (this suite isn't testing that feature).
        this.AddAuthorization();
    }

    private static StoryListingDto MakeStory(int id) =>
        new(id, $"Story {id}", null, null, null, "Author", 5_000,
            StoryStatusEnum.InProgress, Rating.T, DateTime.UtcNow, []);

    private static ProfileHeaderDto MakeHeader(int userId = 1) => new(
        UserId: userId,
        Username: $"User{userId}",
        AvatarUrl: null,
        Tagline: null,
        Badges: [],
        OutgoingVouches: [],
        Stats: null,
        RelationshipState: null,
        ProfileVisibility: ProfileVisibility.Public,
        AllowProfileComments: SocialInteractionPermission.Nobody,
        ShowUserStats: false,
        LastSeenUtc: null);

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

    /// <summary>Renders the page with a story root (the page resolves it via the fake reads).</summary>
    private IRenderedComponent<TreeSearchPage> RenderStoryRootPage(
        TreeSearchListingResultDto? result = null)
    {
        _storyReadService.StoriesById[1] = MakeStory(1);
        _treeSearchService.Result = result ?? MakeResult();
        return Render<TreeSearchPage>(p => p.Add(c => c.StoryId, (int?)1));
    }

    // ── Root-entity header ────────────────────────────────────────────────────────

    [Fact]
    public void StoryRoot_RendersStoryCardHeader()
    {
        IRenderedComponent<TreeSearchPage> cut = RenderStoryRootPage();

        cut.FindComponents<StoryCard>().Should().ContainSingle();
        cut.FindComponents<UserCard>().Should().BeEmpty();
    }

    [Fact]
    public void UserRoot_RendersUserCardHeader()
    {
        _treeSearchService.Result = MakeResult();
        IRenderedComponent<TreeSearchPage> cut = Render<TreeSearchPage>(p => p
            .Add(c => c.UserId, (int?)1));

        cut.FindComponents<UserCard>().Should().ContainSingle();
        cut.FindComponents<StoryCard>().Should().BeEmpty();
    }

    // ── Tab behavior ───────────────────────────────────────────────────────────────

    [Fact]
    public void ExploreTab_RendersExploreComposite_HidesAutomaticControlsAndDeck()
    {
        IRenderedComponent<TreeSearchPage> cut = RenderStoryRootPage();

        cut.FindAll("button[role=tab]")[1].Click(); // Automatic → Explore

        cut.FindComponents<ExploreTab>().Should().ContainSingle();
        cut.FindComponents<DeepDiveTab>().Should().BeEmpty();
        cut.FindComponents<TreeSearchControls>().Should().BeEmpty();
        cut.FindComponents<StoryDeck>().Should().BeEmpty();
    }

    [Fact]
    public void DeepDiveTab_RendersDeepDiveComposite_HidesAutomaticControlsAndDeck()
    {
        IRenderedComponent<TreeSearchPage> cut = RenderStoryRootPage();

        cut.FindAll("button[role=tab]")[2].Click(); // Automatic → Deep Dive

        cut.FindComponents<DeepDiveTab>().Should().ContainSingle();
        cut.FindComponents<ExploreTab>().Should().BeEmpty();
        cut.FindComponents<TreeSearchControls>().Should().BeEmpty();
        cut.FindComponents<StoryDeck>().Should().BeEmpty();
    }

    [Fact]
    public void AutomaticTab_RendersControlsFilterPanelAndDeck()
    {
        IRenderedComponent<TreeSearchPage> cut = RenderStoryRootPage(MakeResult((2, 2, null)));

        cut.FindComponents<TreeSearchControls>().Should().ContainSingle();
        cut.FindComponents<ResultsFilterPanel>().Should().ContainSingle();
        cut.FindComponents<StoryDeck>().Should().ContainSingle();
    }

    // ── Results + degree badge ──────────────────────────────────────────────────────

    [Fact]
    public void Deck_RendersOneCardPerResultItem_WithDegreeBadge()
    {
        IRenderedComponent<TreeSearchPage> cut = RenderStoryRootPage(
            MakeResult((10, 2, null), (11, 4, null)));

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
        IRenderedComponent<TreeSearchPage> cut = RenderStoryRootPage(result);

        cut.Markup.Should().Contain("Showing a sample of many connections");
    }

    // ── Controls Apply re-search ─────────────────────────────────────────────────────

    [Fact]
    public void ControlsApply_RunsAnotherSearch()
    {
        IRenderedComponent<TreeSearchPage> cut = RenderStoryRootPage();
        _treeSearchService.SearchCalls.Should().Be(1, "the page runs the initial traversal on load");

        cut.FindComponent<TreeSearchControls>().Find("button").Click();

        _treeSearchService.SearchCalls.Should().Be(2, "Apply re-runs the traversal with new controls");
    }

    // Mutation sanity: switching tabs changes which components render.
    [Fact]
    public void MutationSanity_SwitchingToExplore_RemovesDeck()
    {
        IRenderedComponent<TreeSearchPage> cut = RenderStoryRootPage(MakeResult((10, 2, null)));

        cut.FindComponents<StoryDeck>().Should().ContainSingle();

        cut.FindAll("button[role=tab]")[1].Click();

        cut.FindComponents<StoryDeck>().Should().BeEmpty();
    }
}

/// <summary>
/// Configurable <see cref="ITreeSearchReadService"/> fake: <see cref="SearchAsync"/> returns the
/// settable <see cref="Result"/> and counts calls; <see cref="TraverseAsync"/> is not exercised
/// by the page and returns an empty result.
/// </summary>
internal sealed class FakeTreeSearchReadService : ITreeSearchReadService
{
    public TreeSearchListingResultDto Result { get; set; } = new()
    {
        Items = [],
        DegreesReached = 0,
        ResultCapTruncated = false,
    };

    public int SearchCalls { get; private set; }

    public Task<TreeSearchResultDto> TraverseAsync(TreeSearchRequest request, CancellationToken ct = default) =>
        Task.FromResult(new TreeSearchResultDto { Hits = [], DegreesReached = 0, ResultCapTruncated = false });

    public Task<TreeSearchListingResultDto> SearchAsync(
        TreeSearchRequest request, StoryFilterDto filter, CancellationToken ct = default)
    {
        SearchCalls++;
        return Task.FromResult(Result);
    }
}
