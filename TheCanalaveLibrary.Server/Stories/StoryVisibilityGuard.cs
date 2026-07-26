using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Shared story- and chapter-visibility predicates — conditionality kind (g), the parent-visibility
/// invariant (<c>identity-and-authorization.md</c> §"Parent-visibility guards", WU-ParentVisibility).
/// <para>
/// A story's children (recommendations, arcs, chapter comments, view totals, interaction rows) must
/// never be more visible than the story. The bare-FK shape these reads used —
/// <c>readDb.Children.Where(c =&gt; c.StoryId == id)</c> — never expands <c>Story</c> into the query,
/// so none of the three <c>Story</c> filters could apply.
/// </para>
/// <para>
/// This guard leans on the filtered <c>readDb.Stories</c> set rather than restating its rules:
/// <c>ContentRating</c>, <c>StoryStatus</c> (whose author clause already makes drafts self-visible),
/// and <c>IsTakenDown</c> all live there. The only thing layered on top is per-story <b>reveal</b>
/// consent, which the filter deliberately cannot express. Status and takedown are confidentiality,
/// never consent — a reveal never bypasses them.
/// </para>
/// </summary>
public static class StoryVisibilityGuard
{
    /// <summary>
    /// True when the viewer may see this story: it survives the read context's rating/status/takedown
    /// filters, or it is rating-gated only and the viewer holds a per-story reveal (or is a verified
    /// bot).
    /// </summary>
    public static async Task<bool> IsStoryVisibleAsync(
        ReadOnlyApplicationDbContext readDb, IActiveUserContext viewer, int storyId)
    {
        // Fast path: the filtered set already applies ContentRating + StoryStatus (author-aware)
        // + IsTakenDown. Most callers land here with a single cheap EXISTS.
        if (await readDb.Stories.AnyAsync(s => s.StoryId == storyId)) return true;

        // Missing the fast path means one of the three filters dropped it. Only the rating gate is
        // consent-bypassable, so re-ask with just that one lifted: if the row still doesn't load,
        // it's absent, non-public status, or taken down — all final.
        var row = await readDb.Stories
            .IgnoreQueryFilters(["ContentRating"]) // elevated read: consent decided post-load
            .Where(s => s.StoryId == storyId)
            .Select(s => new { s.Rating })
            .FirstOrDefaultAsync();

        if (row is null) return false;
        if (viewer.IsVerifiedBot) return true;

        return await RevealCheck.IsRevealedAsync(readDb, viewer, RevealedEntityType.Story, storyId);
    }

    /// <summary>
    /// The <b>confidentiality axis only</b>: the story exists, carries a public lifecycle status (or
    /// the viewer authored it), and has not been taken down — with the viewer's rating ceiling
    /// deliberately ignored.
    /// <para>
    /// The two axes are not the same thing, and <c>ReadOnlyApplicationDbContext</c> says so:
    /// <c>StoryStatus</c>/<c>IsTakenDown</c> are confidentiality and no consent ever bypasses them,
    /// whereas <c>ContentRating</c> is consent and a reveal does. Reads always want the full
    /// <see cref="IsStoryVisibleAsync"/>. This exists for the write paths that are deliberately
    /// rating-permissive — <c>ServerRecommendationWriteService.SubmitAsync</c> has documented since
    /// WU29 that a reader with mature content off may still recommend an M-rated story. Use this only
    /// where such a decision is already recorded; anywhere else, use the full predicate.
    /// </para>
    /// </summary>
    public static Task<bool> IsStoryPublishedAsync(
        ReadOnlyApplicationDbContext readDb, IActiveUserContext viewer, int storyId) =>
        readDb.Stories
            .IgnoreQueryFilters(["ContentRating"]) // deliberate: consent axis not applied here
            .AnyAsync(s => s.StoryId == storyId);

    /// <summary>
    /// True when the viewer may see this chapter: its story is visible AND the chapter is published
    /// (or the viewer authored the story). Mirrors the
    /// <c>(c.IsPublished || c.Story.AuthorId == viewerId)</c> shape used throughout
    /// <c>ServerChapterReadService</c>.
    /// </summary>
    public static async Task<bool> IsChapterVisibleAsync(
        ReadOnlyApplicationDbContext readDb, IActiveUserContext viewer, int chapterId)
    {
        // Deliberate bare-FK projection: Chapter carries no query filter of its own, and touching
        // the Story navigation here would conflate "story hidden" with "chapter missing". The story
        // decision is delegated below instead, where it can be reveal-aware.
        var row = await readDb.Chapters
            .Where(c => c.ChapterId == chapterId)
            .Select(c => new { c.StoryId, c.IsPublished })
            .FirstOrDefaultAsync();

        if (row is null) return false;
        if (!await IsStoryVisibleAsync(readDb, viewer, row.StoryId)) return false;
        if (row.IsPublished) return true;

        // Unpublished chapter — author only. ContentRating is lifted because an author reading their
        // own M-rated story's draft chapter must not be gated by their own display ceiling.
        return viewer.UserId is int uid
               && await readDb.Stories
                   .IgnoreQueryFilters(["ContentRating"]) // elevated read: author's own draft chapter
                   .AnyAsync(s => s.StoryId == row.StoryId && s.AuthorId == uid);
    }
}
