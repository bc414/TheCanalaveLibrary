using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Story export (Feature 54, WU38c). Composes the existing read services — so the content-rating
/// master filter is the only permission gate ("export = what you can read",
/// layer2-services.md §"Export &amp; Import") — normalizes to <see cref="StoryExportModel"/>, and
/// dispatches to the per-format writer. Writers are pure static functions over the model; adding a
/// format = one <see cref="ExportFormat"/> value + one writer + one arm here.
/// </summary>
public class ServerExportService(
    IStoryReadService storyReadService,
    IChapterReadService chapterReadService) : IExportService
{
    public async Task<StoryExportResult?> ExportStoryAsync(int storyId, ExportFormat format)
    {
        StoryDetailsDTO? story = await storyReadService.GetStoryByIdAsync(storyId);
        if (story is null)
        {
            return null; // not found, or filtered out by the viewer's content-rating ceiling
        }

        IReadOnlyList<ChapterExportDto> chapters =
            await chapterReadService.GetChaptersForExportAsync(storyId);

        var model = new StoryExportModel(
            storyId,
            story.StoryTitle ?? "Untitled",
            story.AuthorName ?? "Unknown",
            story.Rating,
            story.LongDescription,
            story.PublishDate,
            story.LastUpdatedDate,
            chapters);

        string slug = StorySlug.Slugify(model.Title);
        if (slug.Length == 0)
        {
            slug = $"story-{storyId}";
        }

        return format switch
        {
            ExportFormat.Epub => new StoryExportResult(
                EpubWriter.Write(model), "application/epub+zip", slug + ".epub"),
            ExportFormat.Pdf => new StoryExportResult(
                PdfWriter.Write(model), "application/pdf", slug + ".pdf"),
            ExportFormat.Html => new StoryExportResult(
                HtmlWriter.Write(model), "text/html", slug + ".html"),
            ExportFormat.Txt => new StoryExportResult(
                TxtWriter.Write(model), "text/plain", slug + ".txt"),
            ExportFormat.Markdown => new StoryExportResult(
                MarkdownWriter.Write(model), "text/markdown", slug + ".md"),
            ExportFormat.Docx => new StoryExportResult(
                DocxWriter.Write(model),
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                slug + ".docx"),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown export format.")
        };
    }
}
