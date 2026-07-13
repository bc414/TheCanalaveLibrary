namespace TheCanalaveLibrary.Core;

/// <summary>
/// Story export (Feature 54): turns an existing story's published chapters into a downloadable
/// file. Read-only — composes <see cref="IStoryReadService"/> + <see cref="IChapterReadService"/>,
/// so the content-rating master filter is the only permission gate ("export = what you can read",
/// layer2-services.md §"Export &amp; Import"). No write half.
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Generates the story as <paramref name="format"/>.
    /// </summary>
    /// <returns>
    /// The generated file, or <c>null</c> when the story doesn't exist or isn't visible to the
    /// current viewer (content-rating filter) — the endpoint maps <c>null</c> to 404.
    /// </returns>
    Task<StoryExportResult?> ExportStoryAsync(int storyId, ExportFormat format);

    /// <summary>
    /// Generates a single published chapter as <paramref name="format"/> (WU45 — the chapter
    /// list's per-row download menu). Same permission model as the story export: what you can
    /// read, you can download.
    /// </summary>
    /// <returns><c>null</c> when the story/chapter doesn't exist, isn't published, or is
    /// filtered by the viewer's content-rating ceiling — mapped to 404.</returns>
    Task<StoryExportResult?> ExportChapterAsync(int storyId, int chapterNumber, ExportFormat format);
}
