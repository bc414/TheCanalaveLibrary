using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="RelatedStoriesSection"/> (Feature 61, WU-RelatedStories). Covers:
/// both decks render from the faked co-occurrence + hydration services; the co-occurrence score
/// never appears in rendered markup; the interaction filter is hidden for anonymous viewers and
/// shown for authenticated ones; toggling the filter re-queries with the new exclusion set; a
/// subsection with zero related stories hides its heading; both-empty renders nothing.
///
/// <b>Tier:</b> RazorComponents (bUnit, no host or DB).
/// </summary>
public class RelatedStoriesSectionTests : BunitContext
{
    private readonly FakeCoOccurrenceReadService _coOccurrence = new();
    private readonly FakeRelatedStoriesStoryReadService _stories = new();
    private readonly FakeRelatedStoriesInteractionReadService _interactions = new();
    private readonly FakeDiscoveryDefaultsReadService _defaults = new();

    public RelatedStoriesSectionTests()
    {
        Services.AddScoped<ICoOccurrenceReadService>(_ => _coOccurrence);
        Services.AddScoped<IStoryReadService>(_ => _stories);
        Services.AddScoped<IUserStoryInteractionReadService>(_ => _interactions);
        Services.AddScoped<IDiscoveryDefaultsReadService>(_ => _defaults);

        // Each StoryDeck card renders StoryCard, which unconditionally nests
        // UserStoryInteractionPanel (own write service), TagChip (sprite resolution), and
        // AddToCustomListMenu (own write service, AuthorizeView-gated) — same registrations as
        // StoryCardTests.
        Services.AddScoped<IUserStoryInteractionWriteService>(_ => new FakeUserStoryInteractionWriteService());
        Services.AddSingleton<ISpriteReadService>(new OptimisticSpriteReadService("/sprites/themes"));
        Services.AddScoped<ICustomListWriteService>(_ => new FakeCustomListWriteService());
        this.AddAuthorization();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static StoryListingDto MakeStory(int id, string title) => new(
        StoryId: id,
        Title: title,
        ShortDescription: null,
        CoverArtRelativeUrl: null,
        AuthorId: 1,
        AuthorName: "Author",
        WordCount: 1000,
        StoryStatusId: StoryStatusEnum.Completed,
        Rating: Rating.E,
        LastUpdatedDate: DateTime.UtcNow,
        Tags: []);

    // ── Both decks render ─────────────────────────────────────────────────────────

    [Fact]
    public void BothSubsections_RenderFromFakedService()
    {
        _coOccurrence.FavoritedResult = [new RelatedStoryScoreDto { RelatedStoryId = 10, Score = 5 }];
        _coOccurrence.RecommendedResult = [new RelatedStoryScoreDto { RelatedStoryId = 20, Score = 3 }];
        _stories.StoriesById = new Dictionary<int, StoryListingDto>
        {
            [10] = MakeStory(10, "Favorited Story"),
            [20] = MakeStory(20, "Recommended Story"),
        };

        IRenderedComponent<RelatedStoriesSection> cut = Render<RelatedStoriesSection>(p => p
            .Add(c => c.StoryId, 1));

        cut.Markup.Should().Contain("Favorited Story").And.Contain("Recommended Story");
        cut.Markup.Should().Contain("Also Favorited").And.Contain("Also Recommended");
    }

    // ── Score never shown ─────────────────────────────────────────────────────────

    [Fact]
    public void Score_NeverAppearsInRenderedMarkup()
    {
        _coOccurrence.FavoritedResult = [new RelatedStoryScoreDto { RelatedStoryId = 10, Score = 424242 }];
        _stories.StoriesById = new Dictionary<int, StoryListingDto> { [10] = MakeStory(10, "Favorited Story") };

        IRenderedComponent<RelatedStoriesSection> cut = Render<RelatedStoriesSection>(p => p
            .Add(c => c.StoryId, 1));

        cut.Markup.Should().NotContain("424242", "the co-occurrence score is server-side ranking only, never displayed");
    }

    // ── Filter gating ─────────────────────────────────────────────────────────────

    [Fact]
    public void Anonymous_NoInteractionFilterRendered()
    {
        _coOccurrence.FavoritedResult = [new RelatedStoryScoreDto { RelatedStoryId = 10, Score = 1 }];
        _stories.StoriesById = new Dictionary<int, StoryListingDto> { [10] = MakeStory(10, "Story") };

        IRenderedComponent<RelatedStoriesSection> cut = Render<RelatedStoriesSection>(p => p
            .Add(c => c.StoryId, 1)
            .Add(c => c.CurrentUserId, (int?)null));

        cut.FindAll("input[type='checkbox']").Should().BeEmpty(
            "anonymous viewers get no interaction filter — exclusions would be inert");
    }

    [Fact]
    public void Authenticated_InteractionFilterRendered()
    {
        _coOccurrence.FavoritedResult = [new RelatedStoryScoreDto { RelatedStoryId = 10, Score = 1 }];
        _stories.StoriesById = new Dictionary<int, StoryListingDto> { [10] = MakeStory(10, "Story") };

        IRenderedComponent<RelatedStoriesSection> cut = Render<RelatedStoriesSection>(p => p
            .Add(c => c.StoryId, 1)
            .Add(c => c.CurrentUserId, 42));

        cut.FindAll("input[type='checkbox']").Should().NotBeEmpty(
            "authenticated viewers get the shared UserStoryInteractionFilter");
    }

    // ── Toggle re-queries ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ToggleFilter_ReQueries_WithTheChosenExclusionSet()
    {
        _coOccurrence.FavoritedResult = [new RelatedStoryScoreDto { RelatedStoryId = 10, Score = 1 }];
        _stories.StoriesById = new Dictionary<int, StoryListingDto>
        {
            [10] = MakeStory(10, "Original Story"),
            [30] = MakeStory(30, "Refiltered Story"),
        };

        IRenderedComponent<RelatedStoriesSection> cut = Render<RelatedStoriesSection>(p => p
            .Add(c => c.StoryId, 1)
            .Add(c => c.CurrentUserId, 42));

        cut.Markup.Should().Contain("Original Story");
        _coOccurrence.FavoritedCalls.Should().ContainSingle();

        // Reconfigure the fake to simulate a different result set for the next query, then
        // toggle the Favorite checkbox (not the seeded Ignore default) to trigger a re-query.
        _coOccurrence.FavoritedResult = [new RelatedStoryScoreDto { RelatedStoryId = 30, Score = 1 }];

        var favoriteCheckbox = cut.FindAll("label")
            .First(l => l.TextContent.Contains("favorited", StringComparison.OrdinalIgnoreCase)
                        && !l.TextContent.Contains("privately", StringComparison.OrdinalIgnoreCase))
            .QuerySelector("input[type='checkbox']")!;

        await favoriteCheckbox.TriggerEventAsync("onchange", new ChangeEventArgs { Value = true });

        cut.Markup.Should().Contain("Refiltered Story");
        cut.Markup.Should().NotContain("Original Story");
        _coOccurrence.FavoritedCalls.Should().HaveCount(2);
        _coOccurrence.FavoritedCalls[1].Excluded.Should().Contain(UserStoryInteractionTypeEnum.Favorite,
            "the toggled kind must be in the exclusion set passed to the re-query");
    }

    // ── Empty-subsection handling ─────────────────────────────────────────────────

    [Fact]
    public void OneSubsectionEmpty_HidesOnlyThatHeading()
    {
        _coOccurrence.FavoritedResult = [];
        _coOccurrence.RecommendedResult = [new RelatedStoryScoreDto { RelatedStoryId = 20, Score = 1 }];
        _stories.StoriesById = new Dictionary<int, StoryListingDto> { [20] = MakeStory(20, "Recommended Story") };

        IRenderedComponent<RelatedStoriesSection> cut = Render<RelatedStoriesSection>(p => p
            .Add(c => c.StoryId, 1));

        cut.Markup.Should().NotContain("Also Favorited",
            "a subsection with zero related stories hides its heading entirely");
        cut.Markup.Should().Contain("Also Recommended");
    }

    [Fact]
    public void BothSubsectionsEmpty_RendersNothing()
    {
        _coOccurrence.FavoritedResult = [];
        _coOccurrence.RecommendedResult = [];

        IRenderedComponent<RelatedStoriesSection> cut = Render<RelatedStoriesSection>(p => p
            .Add(c => c.StoryId, 1));

        cut.Markup.Trim().Should().BeEmpty(
            "when there is nothing to discover from this story, the component renders nothing");
    }
}
