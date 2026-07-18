using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="IChapterWriteService"/> (WU17). Covers: connected-graph insert
/// (Chapter + ChapterContent in one SaveChanges + PrimaryContentId fixup), sanitization (script
/// stripping on save), word-count computation on stripped text, versioning (alternate versions,
/// SortOrder uniqueness, VersionCount), primary-version promotion, and Story.WordCount roll-up.
/// Tier: Integration (real Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class ChapterWriteServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _authorId;
    private int _storyId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        // Story must be owned by the active user — every write method gates on story authorship
        // (MA-301).
        _authorId = await SeedUserAsync();
        _storyId  = await SeedStoryAsync(_authorId);
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorId, showMatureContent: true));
    }

    // --- CreateChapterAsync ---

    [Fact]
    public async Task CreateChapterAsync_InsertsBothChapterAndChapterContentRows()
    {
        int chapterId = await CallCreateAsync(new CreateChapterDto
        {
            StoryId      = _storyId,
            Title        = "Test Chapter",
            ChapterText  = "<p>Hello world</p>",
            Rating       = Rating.E
        });

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Chapter? chapter = await db.Chapters.FirstOrDefaultAsync(c => c.ChapterId == chapterId);
        chapter.Should().NotBeNull();
        chapter!.StoryId.Should().Be(_storyId);
        chapter.ChapterNumber.Should().BeGreaterThan(0);
        chapter.VersionCount.Should().Be(1);

        ChapterContent? content = await db.ChapterContents
            .FirstOrDefaultAsync(cc => cc.ChapterContentId == chapter.PrimaryContentId);
        content.Should().NotBeNull("PrimaryContentId must point at an existing ChapterContent row");
        content!.ChapterId.Should().Be(chapterId);
        content.SortOrder.Should().Be(0);
    }

    [Fact]
    public async Task CreateChapterAsync_WhenTitleBlank_DefaultsToChapterN()
    {
        // First chapter so we know the chapter number will be 1 in a fresh story.
        int freshStoryId = await SeedStoryAsync(_authorId);

        int chapterId = await CallCreateAsync(new CreateChapterDto
        {
            StoryId     = freshStoryId,
            Title       = "  ",    // blank → should default to "Chapter 1"
            ChapterText = "<p>Content.</p>",
            Rating      = Rating.E
        });

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Chapter? chapter = await db.Chapters.FirstOrDefaultAsync(c => c.ChapterId == chapterId);
        chapter.Should().NotBeNull();
        chapter!.Title.Should().Be($"Chapter {chapter.ChapterNumber}");
    }

    [Fact]
    public async Task CreateChapterAsync_SanitizesScriptTag_BeforePersisting()
    {
        // Mutation-sanity target: if the sanitizer allow-list is changed to permit "script", this
        // test will fail because the stored text will contain "<script>" rather than stripping it.
        int chapterId = await CallCreateAsync(new CreateChapterDto
        {
            StoryId     = _storyId,
            Title       = "Sanitize Test",
            ChapterText = "<p>Safe text</p><script>alert('xss')</script>",
            Rating      = Rating.E
        });

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Chapter? chapter = await db.Chapters.FirstOrDefaultAsync(c => c.ChapterId == chapterId);
        ChapterContent? content = await db.ChapterContents
            .FirstOrDefaultAsync(cc => cc.ChapterContentId == chapter!.PrimaryContentId);

        content!.ChapterText.Should().NotContain("<script>");
        content.ChapterText.Should().Contain("Safe text");
    }

    [Fact]
    public async Task CreateChapterAsync_ComputesWordCountFromSanitizedText()
    {
        // Raw HTML: the <script> tag must be stripped before counting; "hello world" = 2 words.
        int chapterId = await CallCreateAsync(new CreateChapterDto
        {
            StoryId     = _storyId,
            Title       = "Word Count Test",
            ChapterText = "<p>hello world</p><script>ignored markup tokens here</script>",
            Rating      = Rating.E
        });

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Chapter? chapter = await db.Chapters.FirstOrDefaultAsync(c => c.ChapterId == chapterId);
        ChapterContent? content = await db.ChapterContents
            .FirstOrDefaultAsync(cc => cc.ChapterContentId == chapter!.PrimaryContentId);

        content!.WordCount.Should().Be(2);  // only "hello" + "world" survive sanitization
    }

    // --- AddAlternateVersionAsync ---

    [Fact]
    public async Task AddAlternateVersionAsync_IncrementsSortOrderAndVersionCount()
    {
        int chapterId = await CallCreateAsync(new CreateChapterDto
        {
            StoryId     = _storyId,
            Title       = "Versioning Test",
            ChapterText = "<p>Primary.</p>",
            Rating      = Rating.E
        });

        long altId = await CallAddAlternateAsync(chapterId, new CreateChapterDto
        {
            StoryId     = _storyId,
            ChapterText = "<p>Alternate version.</p>",
            Rating      = Rating.T
        });

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Chapter? chapter = await db.Chapters.FirstOrDefaultAsync(c => c.ChapterId == chapterId);
        chapter!.VersionCount.Should().Be(2);

        // The alternate version must have SortOrder > 0 (primary is 0), satisfying the unique index.
        ChapterContent? alt = await db.ChapterContents.FirstOrDefaultAsync(cc => cc.ChapterContentId == altId);
        alt!.SortOrder.Should().Be(1);

        // PrimaryContentId must NOT have changed — alternate add does not promote.
        chapter.PrimaryContentId.Should().NotBe(altId);
    }

    // --- SetPrimaryVersionAsync ---

    [Fact]
    public async Task SetPrimaryVersionAsync_RepointersPrimaryContentId()
    {
        int chapterId = await CallCreateAsync(new CreateChapterDto
        {
            StoryId     = _storyId,
            Title       = "Promote Test",
            ChapterText = "<p>Original.</p>",
            Rating      = Rating.E
        });

        long altId = await CallAddAlternateAsync(chapterId, new CreateChapterDto
        {
            StoryId     = _storyId,
            ChapterText = "<p>Now primary.</p>",
            Rating      = Rating.E
        });

        using IServiceScope scope1 = Factory.Services.CreateScope();
        IChapterWriteService writeService = scope1.ServiceProvider.GetRequiredService<IChapterWriteService>();
        await writeService.SetPrimaryVersionAsync(chapterId, altId);

        using IServiceScope scope2 = Factory.Services.CreateScope();
        ApplicationDbContext db = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Chapter? chapter = await db.Chapters.FirstOrDefaultAsync(c => c.ChapterId == chapterId);
        chapter!.PrimaryContentId.Should().Be(altId);
    }

    // --- UpdateChapterContentAsync ---

    [Fact]
    public async Task UpdateChapterContentAsync_UpdatesChapterTextAndRecomputesWordCount()
    {
        int chapterId = await CallCreateAsync(new CreateChapterDto
        {
            StoryId     = _storyId,
            Title       = "Update Test",
            ChapterText = "<p>original</p>",
            Rating      = Rating.E
        });

        using IServiceScope scopeRead = Factory.Services.CreateScope();
        ApplicationDbContext dbRead = scopeRead.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Chapter? chapter = await dbRead.Chapters.FirstOrDefaultAsync(c => c.ChapterId == chapterId);
        long contentId = chapter!.PrimaryContentId!.Value;

        using IServiceScope scopeWrite = Factory.Services.CreateScope();
        IChapterWriteService writeService = scopeWrite.ServiceProvider.GetRequiredService<IChapterWriteService>();
        await writeService.UpdateChapterContentAsync(new UpdateChapterContentDto
        {
            ChapterContentId  = contentId,
            ChapterText       = "<p>one two three four five</p>",
            Rating            = Rating.E
        });

        using IServiceScope scopeVerify = Factory.Services.CreateScope();
        ApplicationDbContext dbVerify = scopeVerify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ChapterContent? updated = await dbVerify.ChapterContents.FirstOrDefaultAsync(cc => cc.ChapterContentId == contentId);
        updated!.WordCount.Should().Be(5);
        updated.ChapterText.Should().Contain("one two three four five");
    }

    // --- MA-301: author gate (regression — these five methods + GetChapterForEditAsync
    // previously performed no ownership check at all) ---

    [Fact]
    public async Task CreateChapterAsync_NonAuthor_ThrowsUnauthorized()
    {
        int otherUserId = await SeedUserAsync("other");
        SetActiveUser(otherUserId);

        Func<Task> act = () => CallCreateAsync(new CreateChapterDto
        {
            StoryId     = _storyId,
            ChapterText = "<p>Should not be allowed.</p>",
            Rating      = Rating.E
        });

        await act.Should().ThrowAsync<UnauthorizedAccessException>(
            "only the story's author may add a chapter to it");
    }

    [Fact]
    public async Task AddAlternateVersionAsync_NonAuthor_ThrowsUnauthorized()
    {
        int chapterId = await CallCreateAsync(new CreateChapterDto
        {
            StoryId     = _storyId,
            ChapterText = "<p>Primary.</p>",
            Rating      = Rating.E
        });

        int otherUserId = await SeedUserAsync("other");
        SetActiveUser(otherUserId);

        Func<Task> act = () => CallAddAlternateAsync(chapterId, new CreateChapterDto
        {
            StoryId     = _storyId,
            ChapterText = "<p>Should not be allowed.</p>",
            Rating      = Rating.E
        });

        await act.Should().ThrowAsync<UnauthorizedAccessException>(
            "only the story's author may add an alternate version");
    }

    [Fact]
    public async Task UpdateChapterContentAsync_NonAuthor_ThrowsUnauthorized()
    {
        int chapterId = await CallCreateAsync(new CreateChapterDto
        {
            StoryId     = _storyId,
            ChapterText = "<p>original</p>",
            Rating      = Rating.E
        });

        using IServiceScope scopeRead = Factory.Services.CreateScope();
        ApplicationDbContext dbRead = scopeRead.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Chapter? chapter = await dbRead.Chapters.FirstOrDefaultAsync(c => c.ChapterId == chapterId);
        long contentId = chapter!.PrimaryContentId!.Value;

        int otherUserId = await SeedUserAsync("other");
        SetActiveUser(otherUserId);

        using IServiceScope scopeWrite = Factory.Services.CreateScope();
        IChapterWriteService writeService = scopeWrite.ServiceProvider.GetRequiredService<IChapterWriteService>();
        Func<Task> act = () => writeService.UpdateChapterContentAsync(new UpdateChapterContentDto
        {
            ChapterContentId = contentId,
            ChapterText      = "<p>tampered</p>",
            Rating           = Rating.E
        });

        await act.Should().ThrowAsync<UnauthorizedAccessException>(
            "only the story's author may edit chapter content");
    }

    [Fact]
    public async Task SetPrimaryVersionAsync_NonAuthor_ThrowsUnauthorized()
    {
        int chapterId = await CallCreateAsync(new CreateChapterDto
        {
            StoryId     = _storyId,
            ChapterText = "<p>Original.</p>",
            Rating      = Rating.E
        });
        long altId = await CallAddAlternateAsync(chapterId, new CreateChapterDto
        {
            StoryId     = _storyId,
            ChapterText = "<p>Alternate.</p>",
            Rating      = Rating.E
        });

        int otherUserId = await SeedUserAsync("other");
        SetActiveUser(otherUserId);

        using IServiceScope scope = Factory.Services.CreateScope();
        IChapterWriteService svc = scope.ServiceProvider.GetRequiredService<IChapterWriteService>();
        Func<Task> act = () => svc.SetPrimaryVersionAsync(chapterId, altId);

        await act.Should().ThrowAsync<UnauthorizedAccessException>(
            "only the story's author may promote a version to primary");
    }

    [Fact]
    public async Task SetPublishedAsync_NonAuthor_ThrowsUnauthorized()
    {
        int chapterId = await CallCreateAsync(new CreateChapterDto
        {
            StoryId     = _storyId,
            ChapterText = "<p>Content.</p>",
            Rating      = Rating.E
        });

        int otherUserId = await SeedUserAsync("other");
        SetActiveUser(otherUserId);

        using IServiceScope scope = Factory.Services.CreateScope();
        IChapterWriteService svc = scope.ServiceProvider.GetRequiredService<IChapterWriteService>();
        Func<Task> act = () => svc.SetPublishedAsync(chapterId, true);

        await act.Should().ThrowAsync<UnauthorizedAccessException>(
            "only the story's author may publish/unpublish a chapter");
    }

    [Fact]
    public async Task GetChapterForEditAsync_NonAuthor_ThrowsUnauthorized()
    {
        int chapterId = await CallCreateAsync(new CreateChapterDto
        {
            StoryId     = _storyId,
            ChapterText = "<p>Draft content only the author should see.</p>",
            Rating      = Rating.E
        });

        using IServiceScope scopeRead = Factory.Services.CreateScope();
        ApplicationDbContext dbRead = scopeRead.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Chapter? chapter = await dbRead.Chapters.FirstOrDefaultAsync(c => c.ChapterId == chapterId);
        long contentId = chapter!.PrimaryContentId!.Value;

        int otherUserId = await SeedUserAsync("other");
        SetActiveUser(otherUserId);

        using IServiceScope scope = Factory.Services.CreateScope();
        IChapterReadService readSvc = scope.ServiceProvider.GetRequiredService<IChapterReadService>();
        Func<Task> act = () => readSvc.GetChapterForEditAsync(contentId);

        await act.Should().ThrowAsync<UnauthorizedAccessException>(
            "the author-only editor read must not leak another author's chapter content");
    }

    [Fact]
    public async Task GetChapterForEditAsync_Author_ReturnsContent()
    {
        int chapterId = await CallCreateAsync(new CreateChapterDto
        {
            StoryId     = _storyId,
            ChapterText = "<p>Author's own content.</p>",
            Rating      = Rating.E
        });

        using IServiceScope scopeRead = Factory.Services.CreateScope();
        ApplicationDbContext dbRead = scopeRead.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Chapter? chapter = await dbRead.Chapters.FirstOrDefaultAsync(c => c.ChapterId == chapterId);
        long contentId = chapter!.PrimaryContentId!.Value;

        using IServiceScope scope = Factory.Services.CreateScope();
        IChapterReadService readSvc = scope.ServiceProvider.GetRequiredService<IChapterReadService>();
        ChapterReadingDto? result = await readSvc.GetChapterForEditAsync(contentId);

        result.Should().NotBeNull("the author may always load their own chapter for editing");
    }

    // --- Story.WordCount roll-up ---

    [Fact]
    public async Task CreateChapterAsync_UpdatesStoryWordCount()
    {
        int freshStoryId = await SeedStoryAsync(_authorId);

        // Two chapters — word counts are additive via the primary-version sum.
        await CallCreateAsync(new CreateChapterDto
        {
            StoryId     = freshStoryId,
            ChapterText = "<p>alpha beta gamma</p>",  // 3 words
            Rating      = Rating.E
        });
        await CallCreateAsync(new CreateChapterDto
        {
            StoryId     = freshStoryId,
            ChapterText = "<p>delta epsilon</p>",     // 2 words
            Rating      = Rating.E
        });

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Story? story = await db.Stories.FindAsync(freshStoryId);
        story!.WordCount.Should().Be(5);
    }

    // --- Validation ---

    [Fact]
    public async Task CreateChapterAsync_ThrowsChapterValidationException_WhenContentEmpty()
    {
        Func<Task> act = async () => await CallCreateAsync(new CreateChapterDto
        {
            StoryId     = _storyId,
            ChapterText = "   ",   // whitespace-only
            Rating      = Rating.E
        });

        await act.Should().ThrowAsync<ChapterValidationException>();
    }

    // --- Rating invariants (Phase 0.5, WU26) ---

    [Fact]
    public async Task CreateChapterAsync_NullRating_IsAllowedAsPrimary()
    {
        // null = inherit story rating; primary invariant passes (effective = story rating).
        int chapterId = await CallCreateAsync(new CreateChapterDto
        {
            StoryId     = _storyId,  // E-rated story
            ChapterText = "<p>Content with inherited rating.</p>",
            Rating      = null       // inherit
        });

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Chapter? chapter = await db.Chapters.FirstOrDefaultAsync(c => c.ChapterId == chapterId);
        ChapterContent? content = await db.ChapterContents
            .FirstOrDefaultAsync(cc => cc.ChapterContentId == chapter!.PrimaryContentId);

        content!.Rating.Should().BeNull("null rating is stored as-is (inherit from story)");
    }

    [Fact]
    public async Task CreateChapterAsync_ExplicitRatingBelowStoryRating_ThrowsFloorViolation()
    {
        // Story is E-rated; floor check: E >= E ✓. But we need a T/M-rated story to test the floor.
        int tRatedStoryId = await SeedStoryAsync(_authorId, rating: Rating.T);

        Func<Task> act = async () => await CallCreateAsync(new CreateChapterDto
        {
            StoryId     = tRatedStoryId,
            ChapterText = "<p>Content.</p>",
            Rating      = Rating.E  // E < T → floor violation
        });

        await act.Should().ThrowAsync<ChapterValidationException>("E-rated version in T-rated story violates floor");
    }

    [Fact]
    public async Task CreateChapterAsync_ExplicitRatingAboveStoryRating_ThrowsPrimaryViolation()
    {
        // First version is always primary. M in T story: floor passes (M >= T), but primary invariant fails (M != T).
        int tRatedStoryId = await SeedStoryAsync(_authorId, rating: Rating.T);

        Func<Task> act = async () => await CallCreateAsync(new CreateChapterDto
        {
            StoryId     = tRatedStoryId,
            ChapterText = "<p>Content.</p>",
            Rating      = Rating.M  // M > T → primary invariant violation
        });

        await act.Should().ThrowAsync<ChapterValidationException>("M-rated first version in T story violates primary invariant");
    }

    [Fact]
    public async Task SetPrimaryVersionAsync_ExplicitRatingAboveStoryRating_ThrowsPrimaryViolation()
    {
        // Promote an M-rated alternate in a T-rated story → primary invariant rejects it.
        int tRatedStoryId = await SeedStoryAsync(_authorId, rating: Rating.T);
        int chapterId = await CallCreateAsync(new CreateChapterDto
        {
            StoryId     = tRatedStoryId,
            ChapterText = "<p>Primary (null/inherit = T).</p>",
            Rating      = null  // inherit = T = story rating ✓
        });

        long altId = await CallAddAlternateAsync(chapterId, new CreateChapterDto
        {
            StoryId     = tRatedStoryId,
            ChapterText = "<p>M alternate.</p>",
            Rating      = Rating.M  // M > T — valid as alternate (floor passes), but cannot be primary
        });

        using IServiceScope scope = Factory.Services.CreateScope();
        IChapterWriteService svc = scope.ServiceProvider.GetRequiredService<IChapterWriteService>();
        Func<Task> act = async () => await svc.SetPrimaryVersionAsync(chapterId, altId);

        await act.Should().ThrowAsync<ChapterValidationException>("M-rated version cannot be made primary in a T story");
    }

    // --- Helpers ---

    private async Task<int> CallCreateAsync(CreateChapterDto dto)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IChapterWriteService svc = scope.ServiceProvider.GetRequiredService<IChapterWriteService>();
        return await svc.CreateChapterAsync(dto);
    }

    private async Task<long> CallAddAlternateAsync(int chapterId, CreateChapterDto dto)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IChapterWriteService svc = scope.ServiceProvider.GetRequiredService<IChapterWriteService>();
        return await svc.AddAlternateVersionAsync(chapterId, dto);
    }

}
