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
}
