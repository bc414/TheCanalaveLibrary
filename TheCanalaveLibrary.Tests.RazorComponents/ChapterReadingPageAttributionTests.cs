using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="ChapterReadingPage"/>'s recommendation-attribution flow (Feature 30,
/// owner ruling D3, WU-InertFeatures): nothing is written on page load — the <c>?rec=</c> direct-link
/// carrier rides <c>MarkStartedAsync</c> at the Ch.1 ≥90% moment, the helpful prompt is fetched only
/// then, Yes records the success and X dismisses, and the widget closes after either.
/// <para>The page's comment thread is stubbed (<c>ComponentFactories.AddStub&lt;CommentSection&gt;</c>) —
/// its own dependency closure is covered by <c>CommentSectionTests</c> and is irrelevant here.</para>
/// Tier: RazorComponents (bUnit, no host or DB; the scroll callback is invoked directly — the JS
/// scroll tracker itself is browser territory, tracker H14).
/// </summary>
public class ChapterReadingPageAttributionTests : BunitContext
{
    private readonly FakeRecommendationWriteService _recs = new();
    private readonly FakeUserStoryInteractionWriteService _interactions = new();
    private readonly BunitAuthorizationContext _auth;

    public ChapterReadingPageAttributionTests()
    {
        Services.AddScoped<IChapterReadService>(_ => new SingleChapterReadService());
        Services.AddScoped<IStoryArcReadService>(_ => new FakeStoryArcReadService());
        Services.AddScoped<IStoryReadService>(_ => new FakeRelatedStoriesStoryReadService());
        Services.AddScoped<IReadingProgressWriteService>(_ => new NoOpReadingProgress());
        Services.AddScoped<IRecommendationReadService>(_ => _recs);
        Services.AddScoped<IRecommendationWriteService>(_ => _recs);
        Services.AddScoped<IUserStoryInteractionWriteService>(_ => _interactions);
        Services.AddScoped<IPublicUrlProvider>(_ => new PublicUrlProvider("https://test.local"));
        Services.AddSingleton<ISpriteReadService>(new OptimisticSpriteReadService("/sprites/themes"));
        ComponentFactories.AddStub<CommentSection>();
        JSInterop.Mode = JSRuntimeMode.Loose;
        _auth = this.AddAuthorization();
    }

    private static RecommendationDto PromptRec() => new(
        5, 1, new UserCardDto(43, "Tastemaker", null, null, []), "<p>Trust me on this one.</p>",
        0, false, false, 0, new DateTime(2026, 1, 1), false, false);

    private void SignIn() =>
        _auth.SetAuthorized("reader").SetClaims(new Claim(ClaimTypes.NameIdentifier, "7"));

    private IRenderedComponent<ChapterReadingPage> RenderChapterOne(string url)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(url);
        IRenderedComponent<ChapterReadingPage> cut = Render<ChapterReadingPage>(p => p
            .Add(c => c.StoryId, 1)
            .Add(c => c.ChapterNumber, 1));
        cut.WaitForAssertion(() => cut.Find("#chapter-body"));
        return cut;
    }

    private static Task ScrollTo(IRenderedComponent<ChapterReadingPage> cut, float progress) =>
        cut.InvokeAsync(() => cut.Instance.OnScrollProgress(progress));

    [Fact]
    public void OnLoad_WithRec_WritesNothing_AndFetchesNoPrompt()
    {
        SignIn();
        RenderChapterOne("/story/1/1?rec=5");

        _interactions.MarkStartedCalls.Should().BeEmpty(
            "D3: the ?rec= carrier is never written on load (the old write FK-failed for every new reader)");
        _recs.GetHelpfulPromptCalls.Should().BeEmpty("the prompt is read at the 90% moment, not on load");
    }

    [Fact]
    public async Task At90PercentOfChapterOne_MarksStartedCarryingTheRec_ThenShowsThePrompt()
    {
        SignIn();
        _recs.SetHelpfulPrompt(PromptRec());
        IRenderedComponent<ChapterReadingPage> cut = RenderChapterOne("/story/1/1?rec=5");

        await ScrollTo(cut, 0.95f);

        _interactions.MarkStartedCalls.Should().Equal([(1, (int?)5)]);
        _recs.GetHelpfulPromptCalls.Should().Equal([1]);
        // The prompt shows the recommendation as a reminder.
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Trust me on this one."));
    }

    [Fact]
    public async Task Yes_RecordsTheSuccess_AndClosesTheWidget()
    {
        SignIn();
        _recs.SetHelpfulPrompt(PromptRec());
        IRenderedComponent<ChapterReadingPage> cut = RenderChapterOne("/story/1/1");
        await ScrollTo(cut, 0.95f);
        cut.WaitForAssertion(() => cut.Find("[aria-label*='Yes']"));

        await cut.Find("[aria-label*='Yes']").ClickAsync(new());

        _recs.RecordSuccessCalls.Should().Equal([5]);
        cut.FindAll("[aria-label='Recommendation feedback']").Should().BeEmpty();
    }

    [Fact]
    public async Task X_DismissesTheAttribution_AndClosesTheWidget()
    {
        SignIn();
        _recs.SetHelpfulPrompt(PromptRec());
        IRenderedComponent<ChapterReadingPage> cut = RenderChapterOne("/story/1/1");
        await ScrollTo(cut, 0.95f);
        cut.WaitForAssertion(() => cut.Find("[aria-label*='Dismiss']"));

        await cut.Find("[aria-label*='Dismiss']").ClickAsync(new());

        _recs.DismissHelpfulPromptCalls.Should().Equal([5]);
        _recs.RecordSuccessCalls.Should().BeEmpty();
        cut.FindAll("[aria-label='Recommendation feedback']").Should().BeEmpty();
    }

    [Fact]
    public async Task TheRecCarrier_IsConsumedOnce_SoAReloadAfterXCannotBringThePromptBack()
    {
        // Review fixes: X deletes the attribution, but the address still carried ?rec= — a reload or Back
        // re-minted the row at the next 90% and the dismissed prompt returned (what D3's X rules out).
        SignIn();
        _recs.SetHelpfulPrompt(PromptRec());
        BunitNavigationManager nav = Services.GetRequiredService<BunitNavigationManager>();
        IRenderedComponent<ChapterReadingPage> cut = RenderChapterOne("/story/1/1?rec=5");

        await ScrollTo(cut, 0.95f);

        nav.Uri.Should().Be("http://localhost/story/1/1", "the carrier is dropped once MarkStarted has used it");
        nav.History.First().Options.ReplaceHistoryEntry.Should().BeTrue(
            "the history entry is replaced, so Back doesn't return to the ?rec= address either");

        cut.WaitForAssertion(() => cut.Find("[aria-label*='Dismiss']"));
        await cut.Find("[aria-label*='Dismiss']").ClickAsync(new());

        // A reload of the current address carries no rec.
        _interactions.MarkStartedCalls.Clear();
        IRenderedComponent<ChapterReadingPage> reloaded = Render<ChapterReadingPage>(p => p
            .Add(c => c.StoryId, 1)
            .Add(c => c.ChapterNumber, 1));
        reloaded.WaitForAssertion(() => reloaded.Find("#chapter-body"));
        await ScrollTo(reloaded, 0.95f);
        _interactions.MarkStartedCalls.Should().Equal([(1, (int?)null)]);
    }

    [Fact]
    public async Task WithoutACarrier_TheAddressIsLeftAlone()
    {
        SignIn();
        BunitNavigationManager nav = Services.GetRequiredService<BunitNavigationManager>();
        IRenderedComponent<ChapterReadingPage> cut = RenderChapterOne("/story/1/1");
        int entries = nav.History.Count;

        await ScrollTo(cut, 0.95f);

        nav.History.Should().HaveCount(entries, "no navigation happens when there is nothing to consume");
    }

    [Fact]
    public async Task AnonymousReader_MarkStartedCarriesNoRec_AndNoPromptIsFetched()
    {
        IRenderedComponent<ChapterReadingPage> cut = RenderChapterOne("/story/1/1?rec=5");

        await ScrollTo(cut, 0.95f);

        _interactions.MarkStartedCalls.Should().Equal([(1, (int?)null)]);
        _recs.GetHelpfulPromptCalls.Should().BeEmpty();
    }

    // ── Local fakes ──────────────────────────────────────────────────────────────

    /// <summary>Serves one published chapter 1 of story 1; everything else empty.</summary>
    private sealed class SingleChapterReadService : IChapterReadService
    {
        public Task<ChapterReadingDto?> GetChapterForReadingAsync(int storyId, int chapterNumber, int? versionOrder = null) =>
            Task.FromResult<ChapterReadingDto?>(new ChapterReadingDto(
                ChapterId: 11, StoryId: 1, ChapterNumber: 1, Title: "The Beginning",
                ChapterText: "<p>Once upon a time.</p>", TopAuthorsNote: null, BottomAuthorsNote: null,
                WordCount: 4, Rating: Rating.E, AuthorId: 42, AuthorName: "Author", VersionOrder: 0,
                VersionName: null, PublishDate: new DateTime(2026, 1, 1), PreviousChapterNumber: null,
                NextChapterNumber: 2, StoryRating: Rating.E));
        public Task<GatedMetadataDto?> GetChapterGateAsync(int storyId, int chapterNumber, int? versionOrder = null) =>
            Task.FromResult<GatedMetadataDto?>(null);
        public Task<IReadOnlyList<ChapterTocEntryDto>> GetChapterTocAsync(int storyId) =>
            Task.FromResult<IReadOnlyList<ChapterTocEntryDto>>([]);
        public Task<IReadOnlyList<ChapterVersionDto>> GetChapterVersionsAsync(int storyId, int chapterNumber) =>
            Task.FromResult<IReadOnlyList<ChapterVersionDto>>([]);
        public Task<IReadOnlyList<ChapterListEntryDto>> GetChapterListAsync(int storyId) =>
            Task.FromResult<IReadOnlyList<ChapterListEntryDto>>([]);
        public Task<DateTime?> GetViewerLastInteractionUtcAsync(int storyId) => Task.FromResult<DateTime?>(null);
        public Task<ChapterReadingDto?> GetChapterForEditAsync(long chapterContentId) =>
            Task.FromResult<ChapterReadingDto?>(null);
        public Task<IReadOnlyList<ChapterExportDto>> GetChaptersForExportAsync(int storyId) =>
            Task.FromResult<IReadOnlyList<ChapterExportDto>>([]);
    }

    private sealed class NoOpReadingProgress : IReadingProgressWriteService
    {
        public Task RecordProgressAsync(int chapterId, float progress) => Task.CompletedTask;
    }
}
