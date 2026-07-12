using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="SpotlightRedemptionPage"/> (Feature 55, WU-Spotlight). Covers:
/// no-slot empty state; both story-pick paths visible (own-recommendation candidates + the
/// StoryTitlePicker search); candidate pick reveals the rec-attach + block steps with the own
/// rec preselected; full blocks disabled; redeem submits the composed DTO.
/// Tier: RazorComponents (bUnit — services faked; server-side validation is the Integration
/// tier's concern).
/// </summary>
public class SpotlightRedemptionPageTests : BunitContext
{
    private readonly FakeSpotlightWriteService _fake = new();
    private readonly FakeRecommendationReadService _fakeRecs = new();

    public SpotlightRedemptionPageTests()
    {
        Services.AddSingleton<ISpotlightWriteService>(_fake);
        Services.AddSingleton<IRecommendationReadService>(_fakeRecs);
        Services.AddSingleton<IToastService, ToastService>();
        // StoryTitlePicker (secondary pick path) injects IStoryReadService.
        Services.AddSingleton<IStoryReadService>(new FakeStoryReadService());
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // ── Fixtures ──────────────────────────────────────────────────────────────────

    private static readonly DateTime BlockStart = SpotlightBlocks.Epoch.AddDays(700);

    private static SpotlightSlotDto MakeSlot(int id = 1) =>
        new(id, SpotlightSlotSource.ModAward, new DateTime(2026, 7, 1));

    private static SpotlightPickCandidateDto MakeCandidate(int recId = 5, int storyId = 7) =>
        new(recId, storyId, "Candidate Story", "OtherAuthor", IsHiddenGem: true, new DateTime(2026, 6, 1));

    private static SpotlightBlockDto MakeBlock(DateTime start, int booked, int capacity = 3) =>
        new(start, start.AddDays(7), booked, capacity);

    private static RecommendationDto MakeRec(int recId, bool isOwn) => new(
        recId, 7, new UserCardDto(99, isOwn ? "Me" : "SomeoneElse", null, null, []),
        "<p>rec</p>", 0, false, false, 0, new DateTime(2026, 6, 1), false, isOwn);

    // ── Empty state ───────────────────────────────────────────────────────────────

    [Fact]
    public void NoSlots_ShowsNoSlotMessage_AndNoBookingForm()
    {
        _fake.Slots = [];

        IRenderedComponent<SpotlightRedemptionPage> cut = Render<SpotlightRedemptionPage>();

        cut.Markup.Should().Contain("You don't have a spotlight slot right now");
        cut.FindComponents<StoryTitlePicker>().Should().BeEmpty();
    }

    // ── Both pick paths offered ───────────────────────────────────────────────────

    [Fact]
    public void WithSlotAndCandidates_ShowsBothStoryPickPaths()
    {
        _fake.Slots = [MakeSlot()];
        _fake.Candidates = [MakeCandidate()];
        _fake.Blocks = [MakeBlock(BlockStart, booked: 0)];

        IRenderedComponent<SpotlightRedemptionPage> cut = Render<SpotlightRedemptionPage>();

        cut.Markup.Should().Contain("From your recommendations", "primary path: own recs/hidden gems");
        cut.Markup.Should().Contain("Candidate Story");
        cut.Markup.Should().Contain("Hidden Gem");
        cut.FindComponents<StoryTitlePicker>().Should().ContainSingle("secondary path: title search");
    }

    // ── Candidate pick reveals rec + block steps ──────────────────────────────────

    [Fact]
    public async Task PickingCandidate_RevealsRecAndBlockSteps_WithOwnRecPreselected()
    {
        _fake.Slots = [MakeSlot()];
        _fake.Candidates = [MakeCandidate(recId: 5, storyId: 7)];
        _fake.Blocks = [MakeBlock(BlockStart, booked: 0), MakeBlock(BlockStart.AddDays(7), booked: 3)];
        _fakeRecs.ForStory = [MakeRec(5, isOwn: true), MakeRec(6, isOwn: false)];

        IRenderedComponent<SpotlightRedemptionPage> cut = Render<SpotlightRedemptionPage>();
        await cut.FindAll("button").First(b => b.TextContent.Contains("Candidate Story")).ClickAsync(new());

        cut.Markup.Should().Contain("Show a recommendation beside it");
        cut.Markup.Should().Contain("(yours)", "the candidate's own recommendation is offered and labeled");
        cut.Markup.Should().Contain("Pick a week");

        // The full block's radio is disabled; the open one is enabled.
        cut.FindAll("input[name='spotlight-block']").Should().HaveCount(2);
        cut.FindAll("input[name='spotlight-block'][disabled]").Should().HaveCount(1, "full blocks can't be picked");
    }

    // ── Redeem submits the composed DTO ───────────────────────────────────────────

    [Fact]
    public async Task Redeem_SubmitsSlotStoryRecAndBlock()
    {
        _fake.Slots = [MakeSlot(id: 11)];
        _fake.Candidates = [MakeCandidate(recId: 5, storyId: 7)];
        _fake.Blocks = [MakeBlock(BlockStart, booked: 1)];
        _fakeRecs.ForStory = [MakeRec(5, isOwn: true)];

        IRenderedComponent<SpotlightRedemptionPage> cut = Render<SpotlightRedemptionPage>();
        await cut.FindAll("button").First(b => b.TextContent.Contains("Candidate Story")).ClickAsync(new());
        await cut.Find("input[name='spotlight-block']").ChangeAsync(new());
        await cut.FindAll("button").First(b => b.TextContent.Contains("Book the spotlight")).ClickAsync(new());

        _fake.LastRedeem.Should().NotBeNull();
        _fake.LastRedeem!.SlotId.Should().Be(11);
        _fake.LastRedeem.StoryId.Should().Be(7);
        _fake.LastRedeem.RecommendationId.Should().Be(5, "the own rec was preselected by the primary path");
        _fake.LastRedeem.BlockStartUtc.Should().Be(BlockStart);
    }

    // ── Fakes ─────────────────────────────────────────────────────────────────────

    private sealed class FakeSpotlightWriteService : ISpotlightWriteService
    {
        public IReadOnlyList<SpotlightSlotDto> Slots { get; set; } = [];
        public IReadOnlyList<SpotlightPickCandidateDto> Candidates { get; set; } = [];
        public IReadOnlyList<SpotlightBlockDto> Blocks { get; set; } = [];
        public RedeemSpotlightSlotDto? LastRedeem { get; private set; }

        public Task<IReadOnlyList<SpotlightDisplayDto>> GetActiveSpotlightsAsync() =>
            Task.FromResult<IReadOnlyList<SpotlightDisplayDto>>([]);
        public Task<IReadOnlyList<SpotlightSlotDto>> GetMyAvailableSlotsAsync() => Task.FromResult(Slots);
        public Task<IReadOnlyList<SpotlightBookingDto>> GetMyBookingsAsync() =>
            Task.FromResult<IReadOnlyList<SpotlightBookingDto>>([]);
        public Task<IReadOnlyList<SpotlightPickCandidateDto>> GetMyPickCandidatesAsync() => Task.FromResult(Candidates);
        public Task<IReadOnlyList<SpotlightBlockDto>> GetBlockAvailabilityAsync() => Task.FromResult(Blocks);

        public Task RedeemSlotAsync(RedeemSpotlightSlotDto dto)
        {
            LastRedeem = dto;
            Slots = []; // consumed — the reload after success shows no remaining slot
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRecommendationReadService : IRecommendationReadService
    {
        public List<RecommendationDto> ForStory { get; set; } = [];

        public Task<List<RecommendationDto>> GetForStoryAsync(int storyId) => Task.FromResult(ForStory);
        public Task<RecommendationDto?> GetByIdAsync(int recommendationId) =>
            Task.FromResult<RecommendationDto?>(null);
        public Task<IReadOnlyList<int>> GetRecommendedStoryIdsAsync() =>
            Task.FromResult<IReadOnlyList<int>>([]);
        public Task<IReadOnlyList<int>> GetHiddenGemStoryIdsAsync() =>
            Task.FromResult<IReadOnlyList<int>>([]);
        public Task<int?> GetHelpfulPromptRecommendationIdAsync(int storyId) => Task.FromResult<int?>(null);
        public Task<IReadOnlyList<int>> GetRecommendedStoryIdsByUserAsync(int userId) =>
            Task.FromResult<IReadOnlyList<int>>([]);
    }
}
