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
    /// Maintains <c>Story.WordCount</c>. (<c>Story.ChapterCount</c> is not yet in the C# model;
    /// it is a post-WU17 schema addition — see <c>audit/Chapters.md</c> Feature 6 Stage note.)
    /// </summary>
    /// <returns>The new <c>Chapter.ChapterId</c>.</returns>
    /// <exception cref="ChapterValidationException">Thrown when DTO validation fails.</exception>
    Task<int> CreateChapterAsync(CreateChapterDto dto);

    /// <summary>
    /// Adds an alternate version (<c>ChapterContent</c> row) to an existing chapter.
    /// Does not change <c>Chapter.PrimaryContentId</c> — use <see cref="SetPrimaryVersionAsync"/>
    /// for that. Increments <c>Chapter.VersionCount</c>. <c>SortOrder</c> is assigned
    /// server-side (max existing + 1).
    /// </summary>
    /// <returns>The new <c>ChapterContent.ChapterContentId</c>.</returns>
    /// <exception cref="ChapterValidationException">Thrown when DTO validation fails.</exception>
    Task<long> AddAlternateVersionAsync(int chapterId, CreateChapterDto dto);

    /// <summary>
    /// Updates the content and metadata of an existing <c>ChapterContent</c> row in place.
    /// If <c>dto.Title</c> is non-null, also updates the parent <c>Chapter.Title</c>.
    /// Sanitizes all HTML fields and recomputes word count. Maintains <c>Story.WordCount</c>.
    /// </summary>
    /// <exception cref="ChapterValidationException">Thrown when DTO validation fails.</exception>
    Task UpdateChapterContentAsync(UpdateChapterContentDto dto);

    /// <summary>
    /// Repoints <c>Chapter.PrimaryContentId</c> to the specified <c>ChapterContent</c> row.
    /// This is the only supported way to change the live version (the Restrict delete edge on
    /// <c>PrimaryContentId</c> means the current primary cannot be deleted until another version
    /// is promoted). Also recomputes <c>Story.WordCount</c> (primary version's word count changes).
    /// </summary>
    /// <exception cref="KeyNotFoundException">Chapter or content row not found.</exception>
    Task SetPrimaryVersionAsync(int chapterId, long chapterContentId);

    /// <summary>
    /// Publishes or unpublishes a chapter. Maintains <c>Story.ChapterCount</c>
    /// (<c>Story.ChapterCount</c> is not in the current C# model — see audit note.)
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
    /// Deletes a chapter and renumbers every later chapter down by one (WU45). Cascades remove
    /// the chapter's contents/comments/read state; <c>Chapter.PrimaryContentId</c>'s Restrict FK
    /// is released first (mirror of the two-step create). StoryArc bounds shrink in the same
    /// transaction; an arc emptied by the deletion is auto-deleted. Refreshes
    /// <c>Story.WordCount</c>. Author-gated.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Chapter not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not the story's author.</exception>
    Task DeleteChapterAsync(int chapterId);
}
