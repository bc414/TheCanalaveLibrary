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

    /// <summary>
    /// Reveal-aware effective ceiling (WU-AccessGate, Direct-navigation plane): a per-story
    /// consent or a verified crawler raises this story's reads to the M ceiling. When elevated,
    /// the caller must ALSO apply <c>IgnoreQueryFilters(["ContentRating"])</c> to its query —
    /// the <c>Story</c> navigation otherwise drops M-story rows at the join regardless of the
    /// per-version rating predicate. One story reveal covers the whole subtree: reading page,
    /// TOC, versions, list, export.
    /// </summary>
    private async Task<(Rating Ceiling, bool Elevated)> EffectiveCeilingAsync(
        ReadOnlyApplicationDbContext readDb, int storyId)
    {
        bool elevated = ActiveUser.IsVerifiedBot
            || await RevealCheck.IsRevealedAsync(readDb, ActiveUser, RevealedEntityType.Story, storyId);
        return (elevated ? Rating.M : ActiveUser.MaxRating, elevated);
    }

    public async Task<ChapterReadingDto?> GetChapterForReadingAsync(
        int storyId,
        int chapterNumber,
        int? versionOrder = null)
    {
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        (Rating ratingCeiling, bool elevated) = await EffectiveCeilingAsync(readDb, storyId);

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

        if (elevated)
            contentQuery = contentQuery.IgnoreQueryFilters(["ContentRating"]); // elevated read: per-story consent / verified crawler

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
        // Draft (unpublished) chapter metadata is author-only (endpoint-authz sweep 2026-07-18):
        // titles/word counts of work-in-progress must not enumerate to other viewers. -1 is an
        // impossible sentinel for the anonymous case (no real user id is negative).
        int viewerId = ActiveUser.UserId ?? -1;

        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        (Rating ratingCeiling, bool elevated) = await EffectiveCeilingAsync(readDb, storyId);

        IQueryable<Chapter> chapters = readDb.Chapters;
        if (elevated)
            chapters = chapters.IgnoreQueryFilters(["ContentRating"]); // elevated read: per-story consent / verified crawler

        return await chapters
            .Where(c => c.StoryId == storyId && (c.IsPublished || c.Story.AuthorId == viewerId))
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
        // Unpublished chapters' version metadata is author-only (endpoint-authz sweep 2026-07-18).
        int viewerId = ActiveUser.UserId ?? -1;

        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        (Rating ratingCeiling, bool elevated) = await EffectiveCeilingAsync(readDb, storyId);

        IQueryable<Chapter> chapterQuery = readDb.Chapters;
        if (elevated)
            chapterQuery = chapterQuery.IgnoreQueryFilters(["ContentRating"]); // elevated read: per-story consent / verified crawler

        // SelectMany from the matching chapter into its accessible ChapterContent rows.
        // OrderBy must be inside the SelectMany (on the entity field) — EF Core cannot translate
        // OrderBy on the projected DTO's VersionOrder property outside the SelectMany boundary.
        return await chapterQuery
            .Where(c => c.StoryId == storyId && c.ChapterNumber == chapterNumber
                     && (c.IsPublished || c.Story.AuthorId == viewerId))
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
        // Draft (unpublished) chapter rows are author-only (endpoint-authz sweep 2026-07-18) —
        // the author's management/story surfaces still see them (AuthorId == viewer), everyone
        // else gets published rows only. -1 is an impossible sentinel for anonymous.
        int chapterViewerId = ActiveUser.UserId ?? -1;

        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        (Rating ratingCeiling, bool elevated) = await EffectiveCeilingAsync(readDb, storyId);

        IQueryable<Chapter> chapterQuery = readDb.Chapters;
        if (elevated)
            chapterQuery = chapterQuery.IgnoreQueryFilters(["ContentRating"]); // elevated read: per-story consent / verified crawler

        // Step 1: All viewer-visible chapters for the story, ordered by ChapterNumber.
        // PrimaryContent is nullable during the brief post-create window; default WordCount to 0.
        List<ChapterRow> chapters = await chapterQuery
            .Where(c => c.StoryId == storyId && (c.IsPublished || c.Story.AuthorId == chapterViewerId))
            .OrderBy(c => c.ChapterNumber)
            .Select(c => new ChapterRow(
                c.ChapterId,
                c.ChapterNumber,
                c.Title,
                c.PrimaryContent != null ? c.PrimaryContent.WordCount : 0,
                c.IsPublished,
                c.PrimaryContent != null ? c.PrimaryContent.PublishDate : null))
            .ToListAsync();

        if (chapters.Count == 0) return [];

        // Step 1b (WU45): the viewer's Feature-44 read state for this story, one query.
        // Anonymous viewers get the empty dictionary → every row projects false/0.
        Dictionary<int, (bool IsRead, float Progress)> readState = [];
        if (ActiveUser.UserId is int viewerId)
        {
            readState = (await readDb.UserChapterInteractions
                    .Where(i => i.UserId == viewerId && i.Chapter.StoryId == storyId)
                    .Select(i => new { i.ChapterId, i.IsRead, i.ReadProgress })
                    .ToListAsync())
                .ToDictionary(x => x.ChapterId, x => (x.IsRead, x.ReadProgress));
        }

        // Step 2: All non-primary alternate versions accessible to the viewer, across all chapters
        // of the story in one query. Mirrors the SelectMany pattern from GetChapterVersionsAsync:
        // the inner OrderBy is on the entity field (cc.SortOrder), not the projected property
        // (EF Core cannot translate OrderBy on a DTO property outside the SelectMany boundary —
        // same rule noted in GetChapterVersionsAsync). References c.PrimaryContentId from the
        // outer scope to exclude the primary — EF Core translates correlated outer-scope refs here
        // the same way GetChapterVersionsAsync references c.PrimaryContentId for the IsPrimary flag.
        List<AltVersionRow> altRows = await chapterQuery
            .Where(c => c.StoryId == storyId && (c.IsPublished || c.Story.AuthorId == chapterViewerId))
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
                c.ChapterId,
                c.ChapterNumber,
                c.Title,
                c.WordCount,
                c.IsPublished,
                c.PublishDate,
                readState.TryGetValue(c.ChapterId, out (bool IsRead, float Progress) rs) && rs.IsRead,
                readState.TryGetValue(c.ChapterId, out (bool IsRead, float Progress) rp) ? rp.Progress : 0f,
                altsByChapter.TryGetValue(c.ChapterNumber, out List<ChapterVersionDto>? alts)
                    ? alts
                    : []))
            .ToList();
    }

    public async Task<DateTime?> GetViewerLastInteractionUtcAsync(int storyId)
    {
        // The "New"-badge watermark (WU45): the viewer's most recent chapter interaction on this
        // story. Null for anonymous viewers or a first-ever visit (no rows) — the strict chain
        // rule then suppresses every New badge (no watermark = nothing is meaningfully "new").
        if (ActiveUser.UserId is not int viewerId) return null;

        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        return await readDb.UserChapterInteractions
            .Where(i => i.UserId == viewerId && i.Chapter.StoryId == storyId)
            .MaxAsync(i => (DateTime?)i.LastInteractionDate);
    }

    public async Task<IReadOnlyList<ChapterExportDto>> GetChaptersForExportAsync(int storyId)
    {
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        // Reveal-aware like the reading paths: export = what you can read, and the reveal rides
        // the prefs cookie on the plain-anchor export GET (bypasses the circuit), so a consented
        // viewer's export works without any special transport (WU-AccessGate).
        (Rating ratingCeiling, bool elevated) = await EffectiveCeilingAsync(readDb, storyId);

        IQueryable<Chapter> chapterQuery = readDb.Chapters;
        if (elevated)
            chapterQuery = chapterQuery.IgnoreQueryFilters(["ContentRating"]); // elevated read: per-story consent / verified crawler

        // Published chapters' primary versions only, viewer's rating ceiling applied
        // (COALESCE(cc.rating, story.rating) — same effective-rating rule as the reading paths).
        // The primary invariant (effective(primary) == story.Rating) means the ceiling rarely
        // bites here, but it stays for defense-in-depth: export must never exceed what reading shows.
        return await chapterQuery
            .Where(c => c.StoryId == storyId && c.IsPublished && c.PrimaryContentId != null)
            .Select(c => c.PrimaryContent!)
            .Where(cc => (cc.Rating ?? cc.Chapter.Story.Rating) <= ratingCeiling)
            .OrderBy(cc => cc.Chapter.ChapterNumber)
            .Select(cc => new ChapterExportDto(
                cc.Chapter.ChapterNumber,
                cc.Chapter.Title,
                cc.ChapterText,
                cc.TopAuthorsNote,
                cc.BottomAuthorsNote))
            .ToListAsync();
    }

    private sealed record ChapterRow(
        int ChapterId, int ChapterNumber, string Title, int WordCount, bool IsPublished,
        DateTime? PublishDate);

    private sealed record AltVersionRow(
        int ChapterNumber, long ChapterContentId, int VersionOrder,
        string? VersionName, Rating? Rating, int WordCount);

    public async Task<ChapterReadingDto?> GetChapterForEditAsync(long chapterContentId)
    {
        // No ShowMatureContent ceiling — authors must be able to open their own chapters for
        // editing regardless of version rating, INCLUDING when the parent Story is M-rated and
        // the author browses mature-off: the ContentRating filter is bypassed below (closes the
        // known WU17 gap — mature-off-author lockout, bug B5, WU-AccessGate Phase 1).
        //
        // Author gate (MA-301): this feeds the author-only editor form, so the story's author is
        // checked here — not just by the client (ChapterEditorPage's own AuthorId comparison is
        // affordance only, not a control; cross-cutting.md "Security vs affordance"). Checked
        // against Story.AuthorId (the story's owner), not ChapterContent.AuthorId (whoever authored
        // that specific version) — the same authority MoveChapterAsync/DeleteChapterAsync use.
        // That ownership gate is what makes the elevated read safe: non-authors get null either way.
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        var row = await readDb.ChapterContents
            // elevated read: author always edits their own chapters regardless of rating setting
            .IgnoreQueryFilters(["ContentRating"])
            .Where(cc => cc.ChapterContentId == chapterContentId)
            .Select(cc => new
            {
                cc.Chapter.Story.AuthorId,
                Dto = new ChapterReadingDto(
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
                    cc.Rating)  // RawRating — null means inherit; needed by the edit form
            })
            .FirstOrDefaultAsync();

        if (row is null) return null;
        // Require authentication explicitly rather than comparing nullables: an anonymous viewer
        // (UserId null) against an authorless story (AuthorId null) would otherwise pass on
        // null == null. RequireAuthorization() on the route blocks that today, but the service is
        // the enforcement point and must not depend on it.
        if (ActiveUser.UserId is not int viewerId || row.AuthorId != viewerId)
            throw new UnauthorizedAccessException("You must be the author of this story.");

        return row.Dto;
    }
}
