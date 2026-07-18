using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="ServerStoryReadService.GetStoryByIdAsync"/> (WU25 extension)
/// covering the new fields added to <see cref="StoryDetailsDTO"/>: <c>AuthorId</c>,
/// <c>CoverArtRelativeUrl</c>, <c>Rating</c>, <c>Status</c>, and sprite-resolved <c>Tags</c>.
/// Also covers <see cref="IChapterReadService.GetChapterListAsync"/> (WU25) — the story-landing-page
/// chapter list with viewer-accessible non-primary alternate versions.
///
/// Tier: <b>Integration</b> (real Testcontainers Postgres via <see cref="IntegrationTestBase"/>).
/// Follows the shared-accumulating-state rule: all assertions use presence / relative order,
/// never absolute position.
/// </summary>
[Collection("Postgres")]
public class StoryDetailTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _authorId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _authorId = await SeedUserAsync("detail-author");
    }

    // ── GetStoryByIdAsync — new fields ──────────────────────────────────────────

    [Fact]
    public async Task GetStoryByIdAsync_PopulatesAuthorId()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());
        int storyId = await SeedStoryAsync(authorId: _authorId, rating: Rating.E);

        StoryDetailsDTO? dto = await GetStoryByIdAsync(storyId);

        dto.Should().NotBeNull();
        dto!.AuthorId.Should().Be(_authorId);
    }

    [Fact]
    public async Task GetStoryByIdAsync_PopulatesRatingAndStatus()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());
        int storyId = await SeedStoryAsync(authorId: _authorId, rating: Rating.T);

        StoryDetailsDTO? dto = await GetStoryByIdAsync(storyId);

        dto.Should().NotBeNull();
        dto!.Rating.Should().Be(Rating.T);
        dto.Status.Should().Be(StoryStatusEnum.InProgress, "SeedStoryAsync seeds InProgress by default");
    }

    [Fact]
    public async Task GetStoryByIdAsync_CoverArtRelativeUrl_IsNull_WhenNotSet()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());
        int storyId = await SeedStoryAsync(authorId: _authorId, rating: Rating.E);

        StoryDetailsDTO? dto = await GetStoryByIdAsync(storyId);

        dto.Should().NotBeNull();
        dto!.CoverArtRelativeUrl.Should().BeNull("SeedStoryAsync does not set a cover URL");
    }

    [Fact]
    public async Task GetStoryByIdAsync_Tags_EmptyWhenNoTagsSeeded()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());
        int storyId = await SeedStoryAsync(authorId: _authorId, rating: Rating.E);

        StoryDetailsDTO? dto = await GetStoryByIdAsync(storyId);

        dto.Should().NotBeNull();
        dto!.Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task GetStoryByIdAsync_Tags_ReturnsSpriteResolvedChips()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());

        // Seed a tag and attach it to the story.
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        string suffix = Guid.NewGuid().ToString("N")[..8];
        Tag tag = new() { TagName = $"DetailTag-{suffix}", TagTypeId = TagTypeEnum.Genre };
        db.Tags.Add(tag);
        await db.SaveChangesAsync();

        Story story = new()
        {
            AuthorId        = _authorId,
            Rating          = Rating.E,
            StoryStatusId   = StoryStatusEnum.InProgress,
            PublishedDate   = DateTime.UtcNow,
            LastUpdatedDate = DateTime.UtcNow,
            StoryListing    = new StoryListing { StoryTitle = $"TaggedStory-{suffix}" },
            StoryDetail     = new StoryDetail  { PostApprovalStatus = StoryStatusEnum.InProgress }
        };
        story.StoryTags.Add(new StoryTag { TagId = tag.TagId });
        db.Stories.Add(story);
        await db.SaveChangesAsync();

        StoryDetailsDTO? dto = await GetStoryByIdAsync(story.StoryId);

        dto.Should().NotBeNull();
        dto!.Tags.Should().ContainSingle(t => t.TagId == tag.TagId && t.TagName == tag.TagName,
            "the seeded tag must appear in the sprite-resolved Tags collection");
    }

    [Fact]
    public async Task GetStoryByIdAsync_ContentRatingFilter_HidesMatureFromNonMatureViewer()
    {
        // Viewer without ShowMatureContent cannot see a Mature story.
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorId, showMatureContent: false));
        int matureId = await SeedStoryAsync(authorId: _authorId, rating: Rating.M);

        StoryDetailsDTO? dto = await GetStoryByIdAsync(matureId);

        dto.Should().BeNull("global ContentRating filter hides M-rated stories from non-mature viewers");
    }

    [Fact]
    public async Task GetStoryByIdAsync_ContentRatingFilter_ShowsMatureToMatureViewer()
    {
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorId, showMatureContent: true));
        int matureId = await SeedStoryAsync(authorId: _authorId, rating: Rating.M);

        StoryDetailsDTO? dto = await GetStoryByIdAsync(matureId);

        dto.Should().NotBeNull();
        dto!.Rating.Should().Be(Rating.M);
    }

    [Fact]
    public async Task GetStoryByIdAsync_NonExistent_ReturnsNull()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());

        StoryDetailsDTO? dto = await GetStoryByIdAsync(storyId: int.MaxValue);

        dto.Should().BeNull();
    }

    // ── GetChapterListAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetChapterListAsync_EmptyStory_ReturnsEmptyList()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());
        int storyId = await SeedStoryAsync();

        IReadOnlyList<ChapterListEntryDto> list = await GetChapterListAsync(storyId);

        list.Should().BeEmpty();
    }

    [Fact]
    public async Task GetChapterListAsync_ReturnsChapters_InChapterNumberOrder()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());
        int storyId = await SeedStoryAsync(_authorId);

        // Create 3 chapters and publish them so they're visible to an anonymous viewer.
        await SeedPublishedChaptersAsync(storyId, count: 3);

        IReadOnlyList<ChapterListEntryDto> list = await GetChapterListAsync(storyId);

        list.Should().HaveCountGreaterThanOrEqualTo(3, "three chapters were seeded");
        // Find the three newly seeded chapters by inspecting relative order.
        int[] chapterNumbers = list.Select(c => c.ChapterNumber).ToArray();
        chapterNumbers.Should().BeInAscendingOrder("chapters must be ordered by ChapterNumber");
    }

    [Fact]
    public async Task GetChapterListAsync_SingleVersionChapter_HasEmptyAlternateVersions()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());
        int storyId = await SeedStoryAsync(_authorId);
        await SeedPublishedChaptersAsync(storyId, count: 1);

        IReadOnlyList<ChapterListEntryDto> list = await GetChapterListAsync(storyId);

        // At least our seeded chapter should appear.
        list.Should().NotBeEmpty();
        // For chapters with only a primary version, AlternateVersions must be empty.
        list.Should().AllSatisfy(c => c.AlternateVersions.Should().BeEmpty(
            "a chapter with only a primary version has no alternates"));
    }

    [Fact]
    public async Task GetChapterListAsync_NonPrimaryAlternate_AppearsInAlternateVersions()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());

        // Seed a story with one chapter that has an E-rated primary + E-rated alternate.
        int storyId = await SeedStoryAsync(rating: Rating.E);
        (int chNum, _, long altId) = await SeedChapterWithAlternateAsync(
            storyId, primaryRating: Rating.E, altRating: Rating.E);

        IReadOnlyList<ChapterListEntryDto> list = await GetChapterListAsync(storyId);

        ChapterListEntryDto? entry = list.FirstOrDefault(c => c.ChapterNumber == chNum);
        entry.Should().NotBeNull();
        entry!.AlternateVersions.Should().ContainSingle(
            v => v.ChapterContentId == altId,
            "the E-rated alternate is within the anonymous viewer's ceiling");
        entry.AlternateVersions.Should().NotContain(
            v => v.IsPrimary, "primary version must never appear in AlternateVersions");
    }

    [Fact]
    public async Task GetChapterListAsync_MatureAlternate_HiddenFromNonMatureViewer()
    {
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorId, showMatureContent: false));

        int storyId = await SeedStoryAsync(rating: Rating.E);
        (int chNum, _, _) = await SeedChapterWithAlternateAsync(
            storyId, primaryRating: Rating.E, altRating: Rating.M);

        IReadOnlyList<ChapterListEntryDto> list = await GetChapterListAsync(storyId);

        ChapterListEntryDto? entry = list.FirstOrDefault(c => c.ChapterNumber == chNum);
        entry.Should().NotBeNull();
        entry!.AlternateVersions.Should().BeEmpty(
            "the M-rated alternate must be hidden from a viewer whose ceiling is T");
    }

    [Fact]
    public async Task GetChapterListAsync_MatureAlternate_VisibleToMatureViewer()
    {
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorId, showMatureContent: true));

        int storyId = await SeedStoryAsync(rating: Rating.E);
        (int chNum, _, long altId) = await SeedChapterWithAlternateAsync(
            storyId, primaryRating: Rating.E, altRating: Rating.M);

        IReadOnlyList<ChapterListEntryDto> list = await GetChapterListAsync(storyId);

        ChapterListEntryDto? entry = list.FirstOrDefault(c => c.ChapterNumber == chNum);
        entry.Should().NotBeNull();
        entry!.AlternateVersions.Should().ContainSingle(
            v => v.ChapterContentId == altId,
            "a mature viewer can see the M-rated alternate");
    }

    [Fact]
    public async Task GetChapterListAsync_UnpublishedChapter_AuthorSeesItWithIsPublishedFalse()
    {
        // Draft rows are author-only server-side since the endpoint-authz sweep (2026-07-18) —
        // the author still gets them with IsPublished=false so the management/story surfaces can
        // render drafts; the anonymous-exclusion side is pinned by ChapterDraftVisibilityTests.
        int authorId = await SeedUserAsync();
        SetActiveUser(authorId);

        int storyId = await SeedStoryAsync(authorId, rating: Rating.E);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Seed an unpublished chapter directly.
        ChapterContent content = new()
        {
            SortOrder = 0, ChapterText = "<p>draft</p>", WordCount = 1,
            Rating = Rating.E, PublishDate = DateTime.UtcNow
        };
        Chapter chapter = new()
        {
            StoryId = storyId, ChapterNumber = 1, Title = "Draft Chapter",
            PrimaryContentId = null, IsPublished = false, VersionCount = 1,
            ChapterContents = [content]
        };
        db.Chapters.Add(chapter);
        await db.SaveChangesAsync();
        chapter.PrimaryContentId = content.ChapterContentId;
        await db.SaveChangesAsync();

        IReadOnlyList<ChapterListEntryDto> list = await GetChapterListAsync(storyId);

        ChapterListEntryDto? draft = list.FirstOrDefault(c => c.ChapterNumber == 1);
        draft.Should().NotBeNull("the service returns unpublished chapters with IsPublished=false");
        draft!.IsPublished.Should().BeFalse();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<StoryDetailsDTO?> GetStoryByIdAsync(int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryReadService svc = scope.ServiceProvider.GetRequiredService<IStoryReadService>();
        return await svc.GetStoryByIdAsync(storyId);
    }

    private async Task<IReadOnlyList<ChapterListEntryDto>> GetChapterListAsync(int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IChapterReadService svc = scope.ServiceProvider.GetRequiredService<IChapterReadService>();
        return await svc.GetChapterListAsync(storyId);
    }

    /// <summary>
    /// Seeds <paramref name="count"/> published chapters (E-rated, single primary version each).
    /// Chapter numbers are auto-assigned by <see cref="IChapterWriteService.CreateChapterAsync"/>.
    /// Chapter writes gate on story authorship (MA-301), so seeding runs as the story's author
    /// (<c>_authorId</c> — callers must seed the story with it) and restores the anonymous viewer
    /// the callers expect before returning.
    /// </summary>
    private async Task SeedPublishedChaptersAsync(int storyId, int count)
    {
        SetActiveUser(_authorId);

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IChapterWriteService svc = scope.ServiceProvider.GetRequiredService<IChapterWriteService>();

        for (int i = 0; i < count; i++)
        {
            // CreateChapterAsync returns the ChapterId (PK), not the ChapterNumber.
            int chapterId = await svc.CreateChapterAsync(new CreateChapterDto
            {
                StoryId = storyId,
                Title = $"Chapter {i + 1}",
                ChapterText = "<p>text</p>",
                Rating = null // inherit story rating
            });
            // SetPublishedAsync takes (chapterId, bool), not (storyId, chapterNumber, bool).
            await svc.SetPublishedAsync(chapterId, isPublished: true);
        }

        SetActiveUser(FakeActiveUserContext.Anonymous());
    }

    /// <summary>
    /// Seeds a published chapter with a primary version (<paramref name="primaryRating"/>) and one
    /// non-primary alternate version (<paramref name="altRating"/>). Returns the chapter number,
    /// primary content id, and alternate content id so tests can assert specific version presence.
    /// Uses direct EF seeding (bypasses write service) to control version ratings precisely.
    /// </summary>
    private async Task<(int ChapterNumber, long PrimaryContentId, long AltContentId)>
        SeedChapterWithAlternateAsync(int storyId, Rating primaryRating, Rating altRating)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        ChapterContent primary = new()
        {
            SortOrder = 0, ChapterText = "<p>primary</p>", WordCount = 1,
            Rating = primaryRating, PublishDate = DateTime.UtcNow
        };
        Chapter chapter = new()
        {
            StoryId = storyId, ChapterNumber = 1, Title = "Multi-Version",
            PrimaryContentId = null, IsPublished = true, VersionCount = 2,
            ChapterContents = [primary]
        };
        db.Chapters.Add(chapter);
        await db.SaveChangesAsync();

        ChapterContent alt = new()
        {
            ChapterId = chapter.ChapterId, SortOrder = 1,
            ChapterText = "<p>alternate</p>", WordCount = 1,
            VersionName = "Alternate", Rating = altRating, PublishDate = DateTime.UtcNow
        };
        db.ChapterContents.Add(alt);
        chapter.PrimaryContentId = primary.ChapterContentId;
        await db.SaveChangesAsync();

        return (chapter.ChapterNumber, primary.ChapterContentId, alt.ChapterContentId);
    }
}
