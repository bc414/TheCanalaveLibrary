using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="IChapterReadService"/> (WU17). Covers: chapter reading
/// projection (primary version, versioned URL, null for non-existent), per-version
/// <c>ShowMatureContent</c> rating ceiling (the global "ContentRating" filter is on Story, not
/// ChapterContent — the read service applies it manually), prev/next navigation, and TOC ordering.
/// Tier: Integration (real Testcontainers Postgres — per-version rating filtering must translate to SQL).
/// </summary>
[Collection("Postgres")]
public class ChapterReadServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _viewerUserId;
    private int _storyId;
    /// <summary>Chapter number (1-based) shared across all tests in this class.</summary>
    private int _chapterNumber;
    private long _primaryContentId;   // E-rated primary version
    private long _altMatureContentId; // M-rated alternate version
    private int _altSortOrder;        // SortOrder of the M-rated alternate

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _viewerUserId = await SeedUserAsync();
        await SeedFixtureChaptersAsync();
    }

    // --- GetChapterForReadingAsync — primary version ---

    [Fact]
    public async Task GetChapterForReadingAsync_PrimaryVersion_ReturnsChapterText()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());

        ChapterReadingDto? dto = await GetForReadingAsync(_storyId, _chapterNumber);

        dto.Should().NotBeNull();
        dto!.ChapterText.Should().Contain("E-rated primary content");
        dto.StoryId.Should().Be(_storyId);
        dto.ChapterNumber.Should().Be(_chapterNumber);
    }

    [Fact]
    public async Task GetChapterForReadingAsync_NonExistentChapter_ReturnsNull()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());

        ChapterReadingDto? dto = await GetForReadingAsync(_storyId, chapterNumber: 99_999);

        dto.Should().BeNull();
    }

    // --- Per-version rating filter (ShowMatureContent ceiling on ChapterContent.Rating) ---

    [Fact]
    public async Task GetChapterForReadingAsync_AnonymousViewer_CannotAccessMRatedAltVersion()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());

        // Request the M-rated alternate by its SortOrder — anonymous viewer gets null.
        ChapterReadingDto? dto = await GetForReadingAsync(_storyId, _chapterNumber, _altSortOrder);

        dto.Should().BeNull("anonymous users cannot see M-rated chapter versions");
    }

    [Fact]
    public async Task GetChapterForReadingAsync_MatureUser_CanAccessMRatedAltVersion()
    {
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_viewerUserId, showMatureContent: true));

        ChapterReadingDto? dto = await GetForReadingAsync(_storyId, _chapterNumber, _altSortOrder);

        dto.Should().NotBeNull();
        dto!.ChapterText.Should().Contain("M-rated alternate content");
        dto.Rating.Should().Be(Rating.M);
    }

    // --- GetChapterVersionsAsync — rating filter on version list ---

    [Fact]
    public async Task GetChapterVersionsAsync_AnonymousViewer_ExcludesMRatedVersions()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());

        IReadOnlyList<ChapterVersionDto> versions = await GetVersionsAsync(_storyId, _chapterNumber);

        versions.Should().NotContain(v => v.Rating == Rating.M,
            "anonymous users cannot see M-rated versions");
        versions.Should().Contain(v => v.ChapterContentId == _primaryContentId);
    }

    [Fact]
    public async Task GetChapterVersionsAsync_MatureUser_IncludesMRatedVersions()
    {
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_viewerUserId, showMatureContent: true));

        IReadOnlyList<ChapterVersionDto> versions = await GetVersionsAsync(_storyId, _chapterNumber);

        versions.Should().Contain(v => v.ChapterContentId == _altMatureContentId);
        versions.Should().Contain(v => v.IsPrimary && v.ChapterContentId == _primaryContentId);
    }

    // --- GetChapterTocAsync — ordering ---

    [Fact]
    public async Task GetChapterTocAsync_ReturnsChapters_InChapterNumberOrder()
    {
        // Seed a fresh story with chapters in reverse-insert order to verify ordering is by
        // ChapterNumber, not insert order (relative-order assertion per testing.md discipline).
        // Chapter writes gate on story authorship (MA-301): seed as the viewer-user, then read
        // back as anonymous.
        int freshStoryId = await SeedStoryAsync(_viewerUserId);

        // Insert three chapters via the write service so ChapterNumbers are assigned correctly.
        await using AsyncServiceScope s1 = Factory.Services.CreateAsyncScope();
        IChapterWriteService write = s1.ServiceProvider.GetRequiredService<IChapterWriteService>();
        SetActiveUser(_viewerUserId);

        int ch1 = await write.CreateChapterAsync(new CreateChapterDto
            { StoryId = freshStoryId, Title = "Alpha",   ChapterText = "<p>a</p>", Rating = Rating.E });
        int ch2 = await write.CreateChapterAsync(new CreateChapterDto
            { StoryId = freshStoryId, Title = "Beta",    ChapterText = "<p>b</p>", Rating = Rating.E });
        int ch3 = await write.CreateChapterAsync(new CreateChapterDto
            { StoryId = freshStoryId, Title = "Gamma",   ChapterText = "<p>c</p>", Rating = Rating.E });

        // Read back as the author: freshly-created chapters are unpublished, and draft toc rows
        // are author-only since the endpoint-authz sweep (2026-07-18; anonymous exclusion is
        // pinned by ChapterDraftVisibilityTests). Ordering is what's under test here.
        IReadOnlyList<ChapterTocEntryDto> toc = await GetTocAsync(freshStoryId);

        // Assert relative ordering — do not assert absolute top-N (shared DB accumulates rows).
        int alphaIndex = toc.Select((e, i) => (e, i)).First(x => x.e.Title == "Alpha").i;
        int betaIndex  = toc.Select((e, i) => (e, i)).First(x => x.e.Title == "Beta").i;
        int gammaIndex = toc.Select((e, i) => (e, i)).First(x => x.e.Title == "Gamma").i;

        alphaIndex.Should().BeLessThan(betaIndex,  "Alpha (Ch 1) must precede Beta (Ch 2)");
        betaIndex.Should().BeLessThan(gammaIndex,  "Beta (Ch 2) must precede Gamma (Ch 3)");

        _ = ch1; _ = ch2; _ = ch3; // suppress unused-variable warnings
    }

    // --- Rating: NULL→story inheritance (Phase 0.5, WU26) ---

    [Fact]
    public async Task GetChapterForReadingAsync_NullRatedVersion_InheritsStoryRatingAsEffective()
    {
        // Seed a T-rated story with a null-rated chapter (inherits T). Reading it as anonymous
        // (ceiling T): effective = null ?? T = T <= T → visible. DTO.Rating must equal T.
        int freshStoryId = await SeedStoryAsync(rating: Rating.T);
        using IServiceScope seedScope = Factory.Services.CreateScope();
        ApplicationDbContext seedDb = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        ChapterContent content = new() { SortOrder = 0, ChapterText = "<p>inherit</p>", WordCount = 1, Rating = null, PublishDate = DateTime.UtcNow };
        Chapter chapter = new() { StoryId = freshStoryId, ChapterNumber = 1, Title = "Ch 1", PrimaryContentId = null, IsPublished = true, VersionCount = 1, ChapterContents = [content] };
        seedDb.Chapters.Add(chapter);
        await seedDb.SaveChangesAsync();
        chapter.PrimaryContentId = content.ChapterContentId;
        await seedDb.SaveChangesAsync();

        SetActiveUser(FakeActiveUserContext.Anonymous());

        ChapterReadingDto? dto = await GetForReadingAsync(freshStoryId, chapterNumber: 1);

        dto.Should().NotBeNull("null rating inherits story's T rating and is within anonymous ceiling");
        dto!.Rating.Should().Be(Rating.T, "effective rating is null ?? T = T");
    }

    // --- Prev/next navigation ---

    [Fact]
    public async Task GetChapterForReadingAsync_IncludesPrevAndNextChapterNumbers()
    {
        // Seed a fresh story with 3 chapters; verify the middle one returns prev = 1, next = 3.
        // Chapter writes gate on story authorship (MA-301): seed as the viewer-user, then read
        // back as anonymous (below, after the publish step).
        int freshStoryId = await SeedStoryAsync(_viewerUserId);
        SetActiveUser(_viewerUserId);

        await using AsyncServiceScope svc = Factory.Services.CreateAsyncScope();
        IChapterWriteService write = svc.ServiceProvider.GetRequiredService<IChapterWriteService>();

        await write.CreateChapterAsync(new CreateChapterDto
            { StoryId = freshStoryId, Title = "Ch 1", ChapterText = "<p>first</p>", Rating = Rating.E });
        await write.CreateChapterAsync(new CreateChapterDto
            { StoryId = freshStoryId, Title = "Ch 2", ChapterText = "<p>middle</p>", Rating = Rating.E });
        await write.CreateChapterAsync(new CreateChapterDto
            { StoryId = freshStoryId, Title = "Ch 3", ChapterText = "<p>last</p>", Rating = Rating.E });

        // Publish all chapters (they're created unpublished; publish to expose them to the reader).
        await using AsyncServiceScope pub = Factory.Services.CreateAsyncScope();
        IChapterWriteService pubWrite = pub.ServiceProvider.GetRequiredService<IChapterWriteService>();
        IReadOnlyList<ChapterTocEntryDto> toc = await GetTocAsync(freshStoryId);
        // Find chapter IDs from the TOC (we need ChapterId to call SetPublished — use a fresh scope).
        // Simpler: seed chapters pre-published using direct EF. Already did it via the write service
        // above; chapters are unpublished by default. For GetChapterForReadingAsync the filter is
        // c.IsPublished — so manually publish them here.
        using IServiceScope dbScope = Factory.Services.CreateScope();
        ApplicationDbContext db = dbScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        List<Chapter> freshChapters = db.Chapters.Where(c => c.StoryId == freshStoryId).ToList();
        foreach (Chapter c in freshChapters) c.IsPublished = true;
        await db.SaveChangesAsync();

        // Read the middle chapter (ChapterNumber == 2) as the anonymous viewer.
        SetActiveUser(FakeActiveUserContext.Anonymous());
        ChapterReadingDto? middle = await GetForReadingAsync(freshStoryId, chapterNumber: 2);

        middle.Should().NotBeNull();
        middle!.PreviousChapterNumber.Should().Be(1);
        middle.NextChapterNumber.Should().Be(3);
    }

    // --- Helpers ---

    private async Task<ChapterReadingDto?> GetForReadingAsync(int storyId, int chapterNumber, int? versionOrder = null)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IChapterReadService svc = scope.ServiceProvider.GetRequiredService<IChapterReadService>();
        return await svc.GetChapterForReadingAsync(storyId, chapterNumber, versionOrder);
    }

    private async Task<IReadOnlyList<ChapterVersionDto>> GetVersionsAsync(int storyId, int chapterNumber)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IChapterReadService svc = scope.ServiceProvider.GetRequiredService<IChapterReadService>();
        return await svc.GetChapterVersionsAsync(storyId, chapterNumber);
    }

    private async Task<IReadOnlyList<ChapterTocEntryDto>> GetTocAsync(int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IChapterReadService svc = scope.ServiceProvider.GetRequiredService<IChapterReadService>();
        return await svc.GetChapterTocAsync(storyId);
    }

    /// <summary>
    /// Seeds a story + two-version chapter (E-rated primary + M-rated alternate) directly via EF
    /// so both ratings exist regardless of the active user's ceiling at insert time.
    /// </summary>
    private async Task SeedFixtureChaptersAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        string suffix = Guid.NewGuid().ToString("N")[..8];
        Story story = new()
        {
            Rating          = Rating.T,
            StoryStatusId   = StoryStatusEnum.InProgress,
            PublishedDate   = DateTime.UtcNow,
            LastUpdatedDate = DateTime.UtcNow,
            StoryListing    = new StoryListing { StoryTitle = $"Read Fixture {suffix}", ShortDescription = "test" },
            StoryDetail     = new StoryDetail { LongDescription = "test", PostApprovalStatus = StoryStatusEnum.InProgress }
        };
        db.Stories.Add(story);
        await db.SaveChangesAsync();
        _storyId = story.StoryId;

        // Primary version (E-rated, SortOrder = 0).
        ChapterContent primary = new()
        {
            SortOrder    = 0,
            ChapterText  = "<p>E-rated primary content</p>",
            WordCount    = 4,
            Rating       = Rating.E,
            PublishDate  = DateTime.UtcNow
        };

        // PrimaryContentId = null: breaks the circular FK dependency; set after first SaveChanges.
        Chapter chapter = new()
        {
            StoryId          = _storyId,
            ChapterNumber    = 1,
            Title            = "Chapter 1",
            PrimaryContentId = null,
            IsPublished      = true,
            VersionCount     = 2,
            ChapterContents  = [primary]
        };
        db.Chapters.Add(chapter);
        await db.SaveChangesAsync();

        // Now add the alternate M-rated version (must be a separate SaveChanges to get the FK id).
        ChapterContent alt = new()
        {
            ChapterId   = chapter.ChapterId,
            SortOrder   = 1,
            ChapterText = "<p>M-rated alternate content</p>",
            WordCount   = 4,
            Rating      = Rating.M,
            PublishDate = DateTime.UtcNow
        };
        db.ChapterContents.Add(alt);
        chapter.PrimaryContentId = primary.ChapterContentId;
        await db.SaveChangesAsync();

        _chapterNumber    = 1;
        _primaryContentId  = primary.ChapterContentId;
        _altMatureContentId = alt.ChapterContentId;
        _altSortOrder      = alt.SortOrder;
    }

}
