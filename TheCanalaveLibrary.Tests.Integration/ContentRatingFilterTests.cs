using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Regression suite for the content-safety filter boundary (post-WU38 revamp: all named display/visibility
/// filters live on <see cref="ReadOnlyApplicationDbContext"/> only; write context sees ground truth).
///
/// <para><b>WU12 block:</b> <c>"ContentRating"</c> filter on <see cref="Story"/> translates to SQL
/// against real Postgres. Also exercises <c>GetListingsByIdsAsync</c> reorder and silent-drop.</para>
///
/// <para><b>IsTakenDown block:</b> taken-down stories are invisible on public reads; the write context
/// can still act on them without any bypass.</para>
///
/// <para><b>Write-path ground-truth block:</b> the line-51 bug — an author with mature content off
/// should be able to edit their own M-rated story. Previously broken because the write context
/// inherited the ContentRating filter.</para>
/// </summary>
[Collection("Postgres")]
public class ContentRatingFilterTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _viewerUserId;
    private int _teenStoryId;
    private int _matureStoryId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _viewerUserId = await SeedUserAsync();
        (_teenStoryId, _matureStoryId) = await SeedFixtureStoriesAsync();
    }

    [Fact]
    public async Task AnonymousViewer_SeesOnlyTeenRatedStories()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());

        StoryListingDto[] listings = await GetListingsAsync([_teenStoryId, _matureStoryId]);

        listings.Select(l => l.StoryId).Should().BeEquivalentTo([_teenStoryId]);
    }

    [Fact]
    public async Task NonMatureUser_SeesOnlyTeenRatedStories()
    {
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_viewerUserId, showMatureContent: false));

        StoryListingDto[] listings = await GetListingsAsync([_teenStoryId, _matureStoryId]);

        listings.Select(l => l.StoryId).Should().BeEquivalentTo([_teenStoryId]);
    }

    [Fact]
    public async Task MatureEnabledUser_SeesBothRatings()
    {
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_viewerUserId, showMatureContent: true));

        StoryListingDto[] listings = await GetListingsAsync([_teenStoryId, _matureStoryId]);

        listings.Select(l => l.StoryId).Should().BeEquivalentTo([_teenStoryId, _matureStoryId]);
    }

    [Fact]
    public async Task GetListingsByIdsAsync_ReordersToInputOrder_AndDropsFilteredOrMissingIds()
    {
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_viewerUserId, showMatureContent: true));
        const int nonExistentId = -999;

        // Deliberately shuffled input order, plus a nonexistent id mixed in — the result must come
        // back reordered to match this exact input order, with the nonexistent id silently dropped
        // (not erred). The rating-based drop is covered separately below.
        StoryListingDto[] listings = await GetListingsAsync([_matureStoryId, nonExistentId, _teenStoryId]);

        listings.Select(l => l.StoryId).Should().Equal(_matureStoryId, _teenStoryId);
    }

    [Fact]
    public async Task GetListingsByIdsAsync_SilentlyDropsMatureStory_WhenCallerCannotSeeIt()
    {
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_viewerUserId, showMatureContent: false));

        // Mature id requested first, but the caller can't see it — must be dropped, not erred, and the
        // remaining teen id must still come back.
        StoryListingDto[] listings = await GetListingsAsync([_matureStoryId, _teenStoryId]);

        listings.Select(l => l.StoryId).Should().Equal(_teenStoryId);
    }

    // ── IsTakenDown filter tests ──────────────────────────────────────────────────

    [Fact]
    public async Task TakenDownStory_IsInvisible_OnPublicRead()
    {
        // Seed a story and take it down directly via the write context.
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Story? story = await db.Stories.FindAsync(_teenStoryId);
            story!.IsTakenDown = true;
            story.TakedownDate = DateTime.UtcNow;
            story.TakedownReason = "test takedown";
            await db.SaveChangesAsync();
        }

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_viewerUserId, showMatureContent: true));

        // The taken-down story must not appear in any public listing.
        StoryListingDto[] listings = await GetListingsAsync([_teenStoryId, _matureStoryId]);
        listings.Select(l => l.StoryId).Should().NotContain(_teenStoryId,
            "taken-down stories must be invisible to public readers");
    }

    [Fact]
    public async Task TakenDownStory_WriteContext_CanStillBeUpdated()
    {
        // Take down the teen story.
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Story? story = await db.Stories.FindAsync(_teenStoryId);
            story!.IsTakenDown = true;
            story.TakedownDate = DateTime.UtcNow;
            story.TakedownReason = "test takedown";
            await db.SaveChangesAsync();
        }

        // Write context must still be able to act on it (no IgnoreQueryFilters needed).
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Story? story = await db.Stories.FindAsync(_teenStoryId);
            story.Should().NotBeNull("write context sees ground truth — taken-down story must be findable");
        }
    }

    // ── Write-path ground-truth tests (the line-51 bug regression) ───────────────

    /// <summary>
    /// Regression for the line-51 bug: before the revamp, the write context inherited the
    /// ContentRating filter. An author with ShowMatureContent=false loading their own M-rated story
    /// via writeDb.Stories got null (filtered) → "Story not found" on their own edit.
    /// After the revamp the write context is unfiltered, so the story is always findable.
    /// </summary>
    [Fact]
    public async Task MatureRatedStory_IsVisible_OnWriteContext_WhenAuthorHasMatureContentOff()
    {
        // Seed an author with ShowMatureContent = false, and an M-rated story they own.
        int authorId = await SeedUserAsync("Author", showMature: false);
        int matureStoryId = await SeedStoryAsync(authorId: authorId, rating: Rating.M);

        // Set active user to that author — mature content OFF (the line-51 scenario).
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(authorId, showMatureContent: false));

        // Exercise the write context directly (the same path as UpdateStoryAsync:51).
        // The story must be found even though the active user has mature content off.
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext writeDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Story? story = await writeDb.Stories.FindAsync(matureStoryId);
        story.Should().NotBeNull(
            "the write context sees ground truth — the M-rated story must be visible " +
            "to the author regardless of their ShowMatureContent setting");
        story!.Rating.Should().Be(Rating.M);
        story.AuthorId.Should().Be(authorId);
    }

    /// <summary>
    /// Companion: the same story must be INVISIBLE on the read context (capped reader sees no M content).
    /// Confirms the read filter still applies after the revamp.
    /// </summary>
    [Fact]
    public async Task MatureRatedStory_IsInvisible_OnReadContext_WhenViewerHasMatureContentOff()
    {
        int authorId = await SeedUserAsync("Author", showMature: false);
        int matureStoryId = await SeedStoryAsync(authorId: authorId, rating: Rating.M);

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(authorId, showMatureContent: false));

        using IServiceScope scope = Factory.Services.CreateScope();
        ReadOnlyApplicationDbContext readDb = scope.ServiceProvider.GetRequiredService<ReadOnlyApplicationDbContext>();

        Story? story = await readDb.Stories.FindAsync(matureStoryId);
        story.Should().BeNull(
            "the read context applies the ContentRating filter — an M-rated story must not " +
            "be returned to a viewer with ShowMatureContent=false, even when they are the author");
    }

    private async Task<StoryListingDto[]> GetListingsAsync(IReadOnlyList<int> ids)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryReadService readService = scope.ServiceProvider.GetRequiredService<IStoryReadService>();
        return await readService.GetListingsByIdsAsync(ids);
    }

    private async Task<(int TeenStoryId, int MatureStoryId)> SeedFixtureStoriesAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext writeDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Direct EF inserts (Add + SaveChanges), not CreateStoryAsync — the content-rating query
        // filter only applies to SELECT-shaped queries, never to Add/SaveChanges, so this is a clean
        // way to seed both ratings regardless of the active user at insert time.
        string suffix = Guid.NewGuid().ToString("N")[..8];
        Story teenStory = NewStory($"Teen Fixture {suffix}", Rating.T);
        Story matureStory = NewStory($"Mature Fixture {suffix}", Rating.M);

        writeDb.Stories.AddRange(teenStory, matureStory);
        await writeDb.SaveChangesAsync();

        return (teenStory.StoryId, matureStory.StoryId);
    }

    private static Story NewStory(string title, Rating rating) => new()
    {
        Rating = rating,
        StoryStatusId = StoryStatusEnum.InProgress,
        PublishedDate = DateTime.UtcNow,
        LastUpdatedDate = DateTime.UtcNow,
        StoryListing = new StoryListing { StoryTitle = title, ShortDescription = "fixture" },
        StoryDetail = new StoryDetail { LongDescription = "fixture", PostApprovalStatus = StoryStatusEnum.InProgress }
    };
}
