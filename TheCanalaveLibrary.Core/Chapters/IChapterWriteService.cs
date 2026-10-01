namespace TheCanalaveLibrary.Core;

/// <summary>
/// Write side of the Chapters service contract. Inherits the read interface so callers that need
/// both read and write inject only the narrowest applicable interface (layer2-services.md
/// §"CQRS-Lite with Inheritance").
/// </summary>
public interface IChapterWriteService : IChapterReadService
{
    /// <summary>
    /// Creates a new chapter (metadata + first version) on a story.
    /// The chapter number is assigned server-side (max existing + 1).
    /// Sanitizes all HTML fields and computes word count before persisting.
    /// The chapter starts unpublished, so <c>Story.WordCount</c> — which sums the primary versions of
    /// <b>published</b> chapters only — does not move until <see cref="SetPublishedAsync"/>.
    /// </summary>
    /// <returns>The new <c>Chapter.ChapterId</c>.</returns>
    /// <exception cref="ChapterValidationException">Thrown when DTO validation fails.</exception>
    Task<int> CreateChapterAsync(CreateChapterDto dto);

    /// <summary>
    /// Adds an alternate version (<c>ChapterContent</c> row) to an existing chapter.
    /// Does not change <c>Chapter.PrimaryContentId</c> — use <see cref="SetPrimaryVersionAsync"/>
    /// for that. Increments <c>Chapter.VersionCount</c>. <c>SortOrder</c> is assigned
    /// server-side (max existing + 1). The increment is an atomic post-commit statement, never a
    /// tracked <c>++</c>.
    /// </summary>
    /// <returns>The new <c>ChapterContent.ChapterContentId</c>.</returns>
    /// <exception cref="ChapterValidationException">Thrown when DTO validation fails.</exception>
    Task<long> AddAlternateVersionAsync(int chapterId, CreateChapterDto dto);

    /// <summary>
    /// Updates the content and metadata of an existing <c>ChapterContent</c> row in place.
    /// If <c>dto.Title</c> is non-null, also updates the parent <c>Chapter.Title</c>.
    /// Sanitizes all HTML fields and recomputes word count. Refreshes <c>Story.WordCount</c> and the
    /// author's <c>WordsWritten</c> (published chapters only — editing a draft moves neither).
    /// </summary>
    /// <exception cref="ChapterValidationException">Thrown when DTO validation fails.</exception>
    Task UpdateChapterContentAsync(UpdateChapterContentDto dto);

    /// <summary>
    /// Repoints <c>Chapter.PrimaryContentId</c> to the specified <c>ChapterContent</c> row.
    /// This is the only supported way to change the live version (the Restrict delete edge on
    /// <c>PrimaryContentId</c> means the current primary cannot be deleted until another version
    /// is promoted). Also refreshes <c>Story.WordCount</c> and the author's <c>WordsWritten</c>
    /// (published chapters only).
    /// </summary>
    /// <exception cref="KeyNotFoundException">Chapter or content row not found.</exception>
    Task SetPrimaryVersionAsync(int chapterId, long chapterContentId);

    /// <summary>
    /// Publishes or unpublishes a chapter (author only). The first publish stamps the chapter's
    /// <c>FirstPublishedDate</c> (D2's publish anchor, never moved afterwards) and each still-unstamped
    /// version's <c>PublishDate</c>; unpublish touches no date. Refreshes <c>Story.WordCount</c> and the
    /// author's <c>WordsWritten</c> (published chapters only), so publishing adds the chapter's words and
    /// unpublishing removes them; a call that changes nothing refreshes to a delta of 0. There is no
    /// stored chapter count — <c>Story.ChapterCount</c> is computed in projections.
    /// <para><b>New-chapter fan-out (WU-InertFeatures):</b> the call that performs the
    /// <c>FirstPublishedDate</c> stamp fires <see cref="INotificationWriteService.NotifyNewChapterAsync"/>
    /// best-effort after its commit — once per chapter, ever (republishing, adding or promoting a
    /// version and editing never notify) — and only while the story is publicly published and not
    /// taken down (a default, <c>roadmap.md</c> row 17).</para>
    /// </summary>
    /// <exception cref="KeyNotFoundException">Chapter not found.</exception>
    Task SetPublishedAsync(int chapterId, bool isPublished);

    /// <summary>
    /// Moves the chapter at <paramref name="fromNumber"/> to <paramref name="toNumber"/>,
    /// renumbering every chapter between them by ±1 (WU45 — drag-to-reorder; creation stays
    /// append-only). Only <c>Chapter.ChapterNumber</c> changes — content, comments, and read
    /// state key on the stable <c>ChapterId</c>. Applies silently to published and draft
    /// chapters alike (link/arc-crossing warnings explicitly waived, WU45 settled). StoryArc
    /// bounds shift in the same transaction (remove-at-from + insert-at-to composition; an arc
    /// emptied by the move is auto-deleted). Author-gated. No-op when from == to.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Story or source chapter not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not the story's author.</exception>
    /// <exception cref="ChapterValidationException">toNumber outside [1, chapter count].</exception>
    Task MoveChapterAsync(int storyId, int fromNumber, int toNumber);

    /// <summary>
    /// Deletes a chapter and renumbers every later chapter down by one (WU45). The chapter's
    /// comments (replies and likes included) are deleted first through their <c>base_comments</c>
    /// rows (<c>TptDelete.ChapterCommentsAsync</c>) — <c>chapter_comments.chapter_id</c> is RESTRICT
    /// and no cascade from a chapter reaches a TPT base row (owner ruling D10). Contents and read
    /// state then cascade with the chapter; <c>Chapter.PrimaryContentId</c>'s Restrict FK
    /// is released first (mirror of the two-step create). StoryArc bounds shrink in the same
    /// transaction; an arc emptied by the deletion is auto-deleted. Refreshes
    /// <c>Story.WordCount</c>. Author-gated.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Chapter not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not the story's author.</exception>
    Task DeleteChapterAsync(int chapterId);
}
