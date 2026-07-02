using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

public class ServerChapterReadService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser) : IChapterReadService
{
    /// <summary>
    /// Exposed as protected so the derived write service can read it without capturing the
    /// constructor parameter a second time (eliminates CS9107 on the write service).
    /// </summary>
    protected IActiveUserContext ActiveUser { get; } = activeUser;
    public async Task<ChapterReadingDto?> GetChapterForReadingAsync(
        int storyId,
        int chapterNumber,
        int? versionOrder = null)
    {
        Rating ratingCeiling = ActiveUser.ShowMatureContent ? Rating.M : Rating.T;

        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();

        // Build a query that resolves to the target ChapterContent. Both branches produce
        // IQueryable<ChapterContent> so the final projection stays unified.
        // Rating ceiling uses COALESCE(cc.rating, story.rating) — null rating inherits story rating.
        IQueryable<ChapterContent> contentQuery = versionOrder.HasValue
            // Specific version by SortOrder — respect the viewer's rating ceiling.
            ? readDb.ChapterContents.Where(cc =>
                cc.Chapter.StoryId == storyId &&
                cc.Chapter.ChapterNumber == chapterNumber &&
                cc.Chapter.IsPublished &&
                cc.SortOrder == versionOrder.Value &&
                (cc.Rating ?? cc.Chapter.Story.Rating) <= ratingCeiling)
            // Primary version (Chapter.PrimaryContentId, nullable during create).
            // Filter out chapters where PrimaryContentId is null (never-committed draft);
            // then navigate to the PrimaryContent row and apply the viewer's rating ceiling.
            // The primary invariant guarantees effective(primary) == story.Rating, so a visible
            // story never returns null on its primary for any ShowMatureContent ceiling.
            : readDb.Chapters
                .Where(c =>
                    c.StoryId == storyId &&
                    c.ChapterNumber == chapterNumber &&
                    c.IsPublished &&
                    c.PrimaryContentId != null)
                .Select(c => c.PrimaryContent!)
                .Where(cc => (cc.Rating ?? cc.Chapter.Story.Rating) <= ratingCeiling);

        // Single projection including correlated subqueries for prev/next and story rating.
        // Rating = effective (COALESCE); RawRating = null (reading page doesn't need raw form value).
        return await contentQuery
            .Select(cc => new ChapterReadingDto(
                cc.ChapterId,
                cc.Chapter.StoryId,
                cc.Chapter.ChapterNumber,
                cc.Chapter.Title,
                cc.ChapterText,
                cc.TopAuthorsNote,
                cc.BottomAuthorsNote,
                cc.WordCount,
                cc.Rating ?? cc.Chapter.Story.Rating,
                cc.AuthorId,
                cc.Author != null ? (cc.Author.UserName ?? "Unknown") : "Unknown",
                cc.SortOrder,
                cc.VersionName,
                cc.PublishDate,
                // Previous chapter: the largest ChapterNumber still less than this one.
                cc.Chapter.Story.Chapters
                    .Where(prev => prev.IsPublished && prev.ChapterNumber < cc.Chapter.ChapterNumber)
                    .OrderByDescending(prev => prev.ChapterNumber)
                    .Select(prev => (int?)prev.ChapterNumber)
                    .FirstOrDefault(),
                // Next chapter: the smallest ChapterNumber still greater than this one.
                cc.Chapter.Story.Chapters
                    .Where(next => next.IsPublished && next.ChapterNumber > cc.Chapter.ChapterNumber)
                    .OrderBy(next => next.ChapterNumber)
                    .Select(next => (int?)next.ChapterNumber)
                    .FirstOrDefault(),
                cc.Chapter.Story.Rating))
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<ChapterTocEntryDto>> GetChapterTocAsync(int storyId)
    {
        Rating ratingCeiling = ActiveUser.ShowMatureContent ? Rating.M : Rating.T;

        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        return await readDb.Chapters
            .Where(c => c.StoryId == storyId)
            .OrderBy(c => c.ChapterNumber)
            .Select(c => new ChapterTocEntryDto(
                c.ChapterNumber,
                c.Title,
                // PrimaryContent is nullable during the brief post-create window; default to 0.
                c.PrimaryContent != null ? c.PrimaryContent.WordCount : 0,
                c.IsPublished,
                // HasAlternateVersions: at least one ChapterContent row other than the primary that
                // the current viewer's rating ceiling permits. Effective rating = COALESCE(cc.rating, story.rating).
                c.ChapterContents.Any(cc =>
                    cc.ChapterContentId != c.PrimaryContentId &&
                    (cc.Rating ?? c.Story.Rating) <= ratingCeiling)))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ChapterVersionDto>> GetChapterVersionsAsync(
        int storyId,
        int chapterNumber)
    {
        Rating ratingCeiling = ActiveUser.ShowMatureContent ? Rating.M : Rating.T;

        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();

        // SelectMany from the matching chapter into its accessible ChapterContent rows.
        // OrderBy must be inside the SelectMany (on the entity field) — EF Core cannot translate
        // OrderBy on the projected DTO's VersionOrder property outside the SelectMany boundary.
        return await readDb.Chapters
            .Where(c => c.StoryId == storyId && c.ChapterNumber == chapterNumber)
            .SelectMany(c => c.ChapterContents
                .Where(cc => (cc.Rating ?? c.Story.Rating) <= ratingCeiling)
                .OrderBy(cc => cc.SortOrder)
                .Select(cc => new ChapterVersionDto(
                    cc.ChapterContentId,
                    cc.SortOrder,
                    cc.VersionName,
                    cc.Rating,  // raw nullable — null means inherit
                    cc.WordCount,
                    cc.ChapterContentId == c.PrimaryContentId)))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ChapterListEntryDto>> GetChapterListAsync(int storyId)
    {
        Rating ratingCeiling = ActiveUser.ShowMatureContent ? Rating.M : Rating.T;

        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();

        // Step 1: All chapters for the story, ordered by ChapterNumber.
        // PrimaryContent is nullable during the brief post-create window; default WordCount to 0.
        List<ChapterRow> chapters = await readDb.Chapters
            .Where(c => c.StoryId == storyId)
            .OrderBy(c => c.ChapterNumber)
            .Select(c => new ChapterRow(
                c.ChapterNumber,
                c.Title,
                c.PrimaryContent != null ? c.PrimaryContent.WordCount : 0,
                c.IsPublished))
            .ToListAsync();

        if (chapters.Count == 0) return [];

        // Step 2: All non-primary alternate versions accessible to the viewer, across all chapters
        // of the story in one query. Mirrors the SelectMany pattern from GetChapterVersionsAsync:
        // the inner OrderBy is on the entity field (cc.SortOrder), not the projected property
        // (EF Core cannot translate OrderBy on a DTO property outside the SelectMany boundary —
        // same rule noted in GetChapterVersionsAsync). References c.PrimaryContentId from the
        // outer scope to exclude the primary — EF Core translates correlated outer-scope refs here
        // the same way GetChapterVersionsAsync references c.PrimaryContentId for the IsPrimary flag.
        List<AltVersionRow> altRows = await readDb.Chapters
            .Where(c => c.StoryId == storyId)
            .SelectMany(c => c.ChapterContents
                .Where(cc => cc.ChapterContentId != c.PrimaryContentId
                          && (cc.Rating ?? c.Story.Rating) <= ratingCeiling)
                .OrderBy(cc => cc.SortOrder)
                .Select(cc => new AltVersionRow(
                    c.ChapterNumber,
                    cc.ChapterContentId,
                    cc.SortOrder,
                    cc.VersionName,
                    cc.Rating,
                    cc.WordCount)))
            .ToListAsync();

        // Step 3: Group alternates by chapter in memory, then combine with the chapter rows.
        Dictionary<int, List<ChapterVersionDto>> altsByChapter = altRows
            .GroupBy(r => r.ChapterNumber)
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => new ChapterVersionDto(
                    r.ChapterContentId, r.VersionOrder, r.VersionName, r.Rating, r.WordCount,
                    IsPrimary: false))  // non-primary by definition (excluded above)
                .ToList());

        return chapters
            .Select(c => new ChapterListEntryDto(
                c.ChapterNumber,
                c.Title,
                c.WordCount,
                c.IsPublished,
                altsByChapter.TryGetValue(c.ChapterNumber, out List<ChapterVersionDto>? alts)
                    ? alts
                    : []))
            .ToList();
    }

    private sealed record ChapterRow(int ChapterNumber, string Title, int WordCount, bool IsPublished);

    private sealed record AltVersionRow(
        int ChapterNumber, long ChapterContentId, int VersionOrder,
        string? VersionName, Rating? Rating, int WordCount);

    public async Task<ChapterReadingDto?> GetChapterForEditAsync(long chapterContentId)
    {
        // No ShowMatureContent ceiling — authors must be able to open their own chapters for
        // editing regardless of version rating. (The writing page (WU26) is responsible for
        // applying IgnoreQueryFilters(["ContentRating"]) if the parent Story is Mature and the
        // editing author has ShowMatureContent=false; WU17 leaves the Story-level filter active
        // here, which suffices for the common case.)
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        return await readDb.ChapterContents
            .Where(cc => cc.ChapterContentId == chapterContentId)
            .Select(cc => new ChapterReadingDto(
                cc.ChapterId,
                cc.Chapter.StoryId,
                cc.Chapter.ChapterNumber,
                cc.Chapter.Title,
                cc.ChapterText,
                cc.TopAuthorsNote,
                cc.BottomAuthorsNote,
                cc.WordCount,
                cc.Rating ?? cc.Chapter.Story.Rating,  // effective rating
                cc.AuthorId,
                cc.Author != null ? (cc.Author.UserName ?? "Unknown") : "Unknown",
                cc.SortOrder,
                cc.VersionName,
                cc.PublishDate,
                null,  // prev/next not needed for the editing surface
                null,
                cc.Chapter.Story.Rating,
                cc.Rating))  // RawRating — null means inherit; needed by the edit form
            .FirstOrDefaultAsync();
    }
}
