using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Service-level regression tests for the draft-chapter visibility gate on
/// <see cref="IChapterReadService.GetChapterTocAsync"/> and
/// <see cref="IChapterReadService.GetChapterListAsync"/> (endpoint-authz sweep 2026-07-18):
/// unpublished chapter metadata (titles, word counts) is author-only — pre-fix, both reads
/// enumerated draft rows to any viewer. The author still sees drafts on their own management/story
/// surfaces (<c>AuthorId == viewer</c>); everyone else (including anonymous) gets published rows
/// only. Tier: Integration (the published-or-author predicate must translate to SQL).
/// </summary>
[Collection("Postgres")]
public class ChapterDraftVisibilityTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private const string PublishedTitle = "Published Chapter";
    private const string DraftTitle     = "Secret Draft Chapter";

    private int _authorId;
    private int _storyId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _authorId = await SeedUserAsync("author");
        _storyId  = await SeedStoryAsync(_authorId);
        await SeedChapterAsync(_storyId, chapterNumber: 1, title: PublishedTitle, isPublished: true);
        await SeedChapterAsync(_storyId, chapterNumber: 2, title: DraftTitle, isPublished: false);
    }

    /// <summary>
    /// Seeds a chapter + its primary <see cref="ChapterContent"/> directly via EF (two-step save to
    /// break the Chapter↔ChapterContent circular FK — same pattern as ChapterReadServiceTests).
    /// </summary>
    private async Task SeedChapterAsync(int storyId, int chapterNumber, string title, bool isPublished)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        ChapterContent content = new()
        {
            SortOrder   = 0,
            ChapterText = $"<p>{title} body</p>",
            WordCount   = 3,
            Rating      = Rating.E,
            PublishDate = DateTime.UtcNow
        };
        Chapter chapter = new()
        {
            StoryId          = storyId,
            ChapterNumber    = chapterNumber,
            Title            = title,
            PrimaryContentId = null,
            IsPublished      = isPublished,
            VersionCount     = 1,
            ChapterContents  = [content]
        };
        db.Chapters.Add(chapter);
        await db.SaveChangesAsync();
        chapter.PrimaryContentId = content.ChapterContentId;
        await db.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<ChapterTocEntryDto>> GetTocAsync(int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IChapterReadService svc = scope.ServiceProvider.GetRequiredService<IChapterReadService>();
        return await svc.GetChapterTocAsync(storyId);
    }

    private async Task<IReadOnlyList<ChapterListEntryDto>> GetListAsync(int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IChapterReadService svc = scope.ServiceProvider.GetRequiredService<IChapterReadService>();
        return await svc.GetChapterListAsync(storyId);
    }

    // ── GetChapterTocAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetChapterTocAsync_Anonymous_ExcludesUnpublishedChapters()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());

        IReadOnlyList<ChapterTocEntryDto> toc = await GetTocAsync(_storyId);

        toc.Select(e => e.Title).Should().Contain(PublishedTitle);
        toc.Select(e => e.Title).Should().NotContain(DraftTitle,
            "draft chapter metadata (titles, word counts) must not enumerate to non-author viewers " +
            "(endpoint-authz sweep 2026-07-18)");
    }

    [Fact]
    public async Task GetChapterTocAsync_Author_IncludesUnpublishedChapters()
    {
        SetActiveUser(_authorId);

        IReadOnlyList<ChapterTocEntryDto> toc = await GetTocAsync(_storyId);

        toc.Select(e => e.Title).Should().Contain([PublishedTitle, DraftTitle],
            "the author sees their own drafts in the TOC");
    }

    // ── GetChapterListAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetChapterListAsync_Anonymous_ExcludesUnpublishedChapters()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());

        IReadOnlyList<ChapterListEntryDto> list = await GetListAsync(_storyId);

        list.Select(e => e.Title).Should().Contain(PublishedTitle);
        list.Select(e => e.Title).Should().NotContain(DraftTitle,
            "draft chapter rows are author-only in the chapter list (endpoint-authz sweep 2026-07-18)");
    }

    [Fact]
    public async Task GetChapterListAsync_Author_IncludesUnpublishedChapters()
    {
        SetActiveUser(_authorId);

        IReadOnlyList<ChapterListEntryDto> list = await GetListAsync(_storyId);

        list.Select(e => e.Title).Should().Contain([PublishedTitle, DraftTitle],
            "the author sees their own drafts in the chapter list");
    }
}
