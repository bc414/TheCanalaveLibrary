using AngleSharp.Html.Parser;
using TheCanalaveLibrary.Core;
using VersOne.Epub;
using VersOne.Epub.Schema;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// EPUB → per-chapter HTML (WU38d) via VersOne.Epub — chosen over hand-rolling because wild files
/// (AO3, Calibre, FicHub) are far messier than the EPUBs we generate. Spine reading order defines
/// the chapter candidates; titles come from the navigation document (path-matched), falling back
/// to the first heading in the content. Whitespace-only spine items are skipped; title pages/TOCs
/// usually carry some text and surface in review for the author to drop.
/// </summary>
public static class EpubImportReader
{
    public sealed record EpubChapter(string? Title, string BodyHtml);

    public sealed record EpubContent(string? BookTitle, string? BookAuthor, IReadOnlyList<EpubChapter> Chapters);

    public static async Task<EpubContent> ReadAsync(Stream file)
    {
        EpubBook book;
        try
        {
            book = await EpubReader.ReadBookAsync(file);
        }
        catch (Exception ex)
        {
            throw new ImportException("This file couldn't be read as an EPUB.", ex);
        }

        if (book.ReadingOrder.Count > ImportLimits.MaxEpubChapters)
        {
            throw new ImportException(
                $"This EPUB has more than {ImportLimits.MaxEpubChapters} content files — that's beyond what an import can handle.");
        }

        Dictionary<string, string> navTitlesByPath = BuildNavTitleMap(book);
        var parser = new HtmlParser();
        var chapters = new List<EpubChapter>();

        foreach (EpubLocalTextContentFile item in book.ReadingOrder)
        {
            var document = parser.ParseDocument(item.Content);
            string body = document.Body?.InnerHtml ?? string.Empty;
            if (document.Body is null || document.Body.TextContent.Trim().Length == 0)
            {
                continue; // whitespace-only spine item (blank separator pages)
            }

            navTitlesByPath.TryGetValue(NormalizePath(item.FilePath), out string? navTitle);
            string? title = navTitle
                ?? document.Body.QuerySelector("h1, h2, h3")?.TextContent.Trim();

            chapters.Add(new EpubChapter(string.IsNullOrWhiteSpace(title) ? null : title, body));
        }

        return new EpubContent(
            string.IsNullOrWhiteSpace(book.Title) ? null : book.Title,
            string.IsNullOrWhiteSpace(book.Author) ? null : book.Author,
            chapters);
    }

    private static Dictionary<string, string> BuildNavTitleMap(EpubBook book)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void Walk(IEnumerable<EpubNavigationItem> items)
        {
            foreach (EpubNavigationItem item in items)
            {
                if (item.Link?.ContentFilePath is { Length: > 0 } path && item.Title.Length > 0)
                {
                    map.TryAdd(NormalizePath(path), item.Title);
                }
                if (item.NestedItems.Count > 0)
                {
                    Walk(item.NestedItems);
                }
            }
        }

        if (book.Navigation is not null)
        {
            Walk(book.Navigation);
        }
        return map;
    }

    /// <summary>Nav hrefs and spine paths can differ in leading directories — compare by tail.</summary>
    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').TrimStart('/').ToLowerInvariant();
}
