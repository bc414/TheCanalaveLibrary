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
    private int _storyId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _storyId = await SeedStoryAsync();

        // Set the fake user to authenticated so ActiveUser.UserId is non-null.
        // AuthorId on ChapterContent is nullable, but using a real FK value is more realistic.
        int testUserId = await SeedUserAsync();
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(testUserId, showMatureContent: true));
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
        int freshStoryId = await SeedStoryAsync();

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

    // --- Story.WordCount roll-up ---

    [Fact]
    public async Task CreateChapterAsync_UpdatesStoryWordCount()
    {
        int freshStoryId = await SeedStoryAsync();

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
