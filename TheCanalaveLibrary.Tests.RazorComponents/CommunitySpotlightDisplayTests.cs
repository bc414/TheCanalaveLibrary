using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="CommunitySpotlightDisplay"/> (Feature 55, WU-Spotlight) — the
/// homepage spotlight section. Covers: empty state; placement with an attached recommendation
/// (StoryCard + RecommendationCard side by side); placement without one (rec half stays blank —
/// the settled requirement); multiple placements.
/// Tier: RazorComponents (bUnit, no host or DB — <see cref="ISpotlightReadService"/> faked).
/// </summary>
public class CommunitySpotlightDisplayTests : BunitContext
{
    private readonly FakeSpotlightReadService _fakeSpotlights = new();

    public CommunitySpotlightDisplayTests()
    {
        Services.AddSingleton<ISpotlightReadService>(_fakeSpotlights);
        // Nested StoryCard's own dependency set (the StoryCardTests registrations):
        Services.AddScoped<IUserStoryInteractionWriteService>(_ => new FakeUserStoryInteractionWriteService());
        Services.AddSingleton<ISpriteReadService>(new OptimisticSpriteReadService("/sprites/themes"));
        Services.AddSingleton<IStoryReadService>(new FakeStoryReadService());
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // ── Fixtures ──────────────────────────────────────────────────────────────────

    private static StoryListingDto MakeStory(int storyId = 7, string title = "Spotlit Story") =>
        new(storyId, title, "short", null, 42, "StoryAuthor", 5_000,
            StoryStatusEnum.InProgress, Rating.E, DateTime.UtcNow, []);

    private static RecommendationDto MakeRec(int recId = 3) => new(
        RecommendationId: recId,
        StoryId: 7,
        Recommender: new UserCardDto(99, "SponsorUser", null, null, []),
        BodyHtml: "<p>You should read this.</p>",
        LikeCount: 0,
        IsHiddenGem: false,
        IsHighlightedByAuthor: false,
        SuccessfulRecCount: 0,
        DatePosted: new DateTime(2026, 7, 1),
        IsLikedByCurrentUser: false,
        IsOwnRecommendation: false);

    private static SpotlightDisplayDto MakeSpotlight(int id, StoryListingDto story, RecommendationDto? rec) =>
        new(id, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(6), story, rec);

    // ── Empty state ───────────────────────────────────────────────────────────────

    [Fact]
    public void NoActiveSpotlights_RendersEmptyState()
    {
        _fakeSpotlights.Active = [];

        IRenderedComponent<CommunitySpotlightDisplay> cut = Render<CommunitySpotlightDisplay>();

        cut.Markup.Should().Contain("Community Spotlight");
        cut.Markup.Should().Contain("No stories are in the spotlight right now");
    }

    // ── With an attached recommendation ──────────────────────────────────────────

    [Fact]
    public void PlacementWithRecommendation_RendersStoryCardAndRecommendationCard()
    {
        _fakeSpotlights.Active = [MakeSpotlight(1, MakeStory(title: "Spotlit Story"), MakeRec())];

        IRenderedComponent<CommunitySpotlightDisplay> cut = Render<CommunitySpotlightDisplay>();

        cut.Markup.Should().Contain("Spotlit Story", "the story card renders");
        cut.Markup.Should().Contain("SponsorUser", "the recommendation card carries its recommender attribution");
        cut.Markup.Should().Contain("You should read this.", "the recommendation body renders");
    }

    // ── Blank rec half ────────────────────────────────────────────────────────────

    [Fact]
    public void PlacementWithoutRecommendation_RecHalfStaysBlank()
    {
        _fakeSpotlights.Active = [MakeSpotlight(1, MakeStory(title: "Story Alone"), rec: null)];

        IRenderedComponent<CommunitySpotlightDisplay> cut = Render<CommunitySpotlightDisplay>();

        cut.Markup.Should().Contain("Story Alone");
        cut.FindComponents<RecommendationCard>().Should().BeEmpty(
            "no recommendation attached → the rec half stays blank (settled requirement)");
    }

    // ── Multiple placements ───────────────────────────────────────────────────────

    [Fact]
    public void MultiplePlacements_AllRender()
    {
        _fakeSpotlights.Active =
        [
            MakeSpotlight(1, MakeStory(storyId: 7, title: "First Spotlight"), MakeRec()),
            MakeSpotlight(2, MakeStory(storyId: 8, title: "Second Spotlight"), rec: null),
        ];

        IRenderedComponent<CommunitySpotlightDisplay> cut = Render<CommunitySpotlightDisplay>();

        cut.Markup.Should().Contain("First Spotlight");
        cut.Markup.Should().Contain("Second Spotlight");
        cut.FindComponents<StoryCard>().Count.Should().Be(2);
        cut.FindComponents<RecommendationCard>().Count.Should().Be(1);
    }

    // ── Fake ──────────────────────────────────────────────────────────────────────

    private sealed class FakeSpotlightReadService : ISpotlightReadService
    {
        public IReadOnlyList<SpotlightDisplayDto> Active { get; set; } = [];

        public Task<IReadOnlyList<SpotlightDisplayDto>> GetActiveSpotlightsAsync() => Task.FromResult(Active);
        public Task<IReadOnlyList<SpotlightSlotDto>> GetMyAvailableSlotsAsync() =>
            Task.FromResult<IReadOnlyList<SpotlightSlotDto>>([]);
        public Task<IReadOnlyList<SpotlightBookingDto>> GetMyBookingsAsync() =>
            Task.FromResult<IReadOnlyList<SpotlightBookingDto>>([]);
        public Task<IReadOnlyList<SpotlightPickCandidateDto>> GetMyPickCandidatesAsync() =>
            Task.FromResult<IReadOnlyList<SpotlightPickCandidateDto>>([]);
        public Task<IReadOnlyList<SpotlightBlockDto>> GetBlockAvailabilityAsync() =>
            Task.FromResult<IReadOnlyList<SpotlightBlockDto>>([]);
    }
}
