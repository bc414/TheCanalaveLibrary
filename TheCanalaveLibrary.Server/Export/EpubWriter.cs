using System.IO.Compression;
using System.Net;
using System.Text;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Xhtml;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// EPUB 3 export (WU38c) — zero-dependency, built directly with <see cref="ZipArchive"/> (an EPUB
/// is a ZIP of XHTML + manifest). Spec constraints honored here:
/// <list type="bullet">
/// <item><c>mimetype</c> must be the FIRST entry and STORED (uncompressed) — readers sniff it at a
/// fixed byte offset.</item>
/// <item>Content documents must be well-formed XHTML — stored chapter HTML has bare <c>&lt;br&gt;</c>
/// and optional end tags, so bodies are re-serialized through AngleSharp's XHTML formatter.</item>
/// <item>Identifier is stable per story (<c>urn:canalave:story:{id}</c>-less here — title-scoped
/// deterministic id derived by the caller's model; no random GUIDs, exports are reproducible).</item>
/// </list>
/// Import counterpart is <c>VersOne.Epub</c> (wild files are messier than what we generate).
/// </summary>
public static class EpubWriter
{
    public static byte[] Write(StoryExportModel story)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            // 1. mimetype — first entry, stored uncompressed (EPUB OCF requirement).
            ZipArchiveEntry mimetype = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
            using (var writer = new StreamWriter(mimetype.Open(), new UTF8Encoding(false)))
            {
                writer.Write("application/epub+zip");
            }

            // 2. OCF container pointing at the package document.
            WriteEntry(zip, "META-INF/container.xml",
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
                  <rootfiles>
                    <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/>
                  </rootfiles>
                </container>
                """);

            // 3. Content documents.
            WriteEntry(zip, "OEBPS/title.xhtml", BuildTitlePage(story));
            for (int i = 0; i < story.Chapters.Count; i++)
            {
                WriteEntry(zip, $"OEBPS/chapter-{i + 1}.xhtml", BuildChapterPage(story.Chapters[i]));
            }

            // 4. Navigation document + package manifest/spine.
            WriteEntry(zip, "OEBPS/nav.xhtml", BuildNav(story));
            WriteEntry(zip, "OEBPS/content.opf", BuildOpf(story));
        }

        return buffer.ToArray();
    }

    private static void WriteEntry(ZipArchive zip, string path, string content)
    {
        ZipArchiveEntry entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string BuildOpf(StoryExportModel story)
    {
        var sb = new StringBuilder();
        sb.Append("""
            <?xml version="1.0" encoding="UTF-8"?>
            <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="pub-id">
              <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
            """);
        sb.Append($"    <dc:identifier id=\"pub-id\">urn:canalave:story:{story.StoryId}</dc:identifier>\n");
        sb.Append($"    <dc:title>{XmlEscape(story.Title)}</dc:title>\n");
        sb.Append($"    <dc:creator>{XmlEscape(story.AuthorName)}</dc:creator>\n");
        sb.Append("    <dc:language>en</dc:language>\n");
        sb.Append($"    <meta property=\"dcterms:modified\">{story.LastUpdatedDate:yyyy-MM-ddTHH:mm:ss}Z</meta>\n");
        sb.Append("  </metadata>\n  <manifest>\n");
        sb.Append("    <item id=\"nav\" href=\"nav.xhtml\" media-type=\"application/xhtml+xml\" properties=\"nav\"/>\n");
        sb.Append("    <item id=\"title\" href=\"title.xhtml\" media-type=\"application/xhtml+xml\"/>\n");
        for (int i = 1; i <= story.Chapters.Count; i++)
        {
            sb.Append($"    <item id=\"chapter-{i}\" href=\"chapter-{i}.xhtml\" media-type=\"application/xhtml+xml\"/>\n");
        }
        sb.Append("  </manifest>\n  <spine>\n");
        sb.Append("    <itemref idref=\"title\"/>\n");
        for (int i = 1; i <= story.Chapters.Count; i++)
        {
            sb.Append($"    <itemref idref=\"chapter-{i}\"/>\n");
        }
        sb.Append("  </spine>\n</package>\n");
        return sb.ToString();
    }

    private static string BuildNav(StoryExportModel story)
    {
        var sb = new StringBuilder();
        sb.Append(XhtmlHead("Contents"));
        sb.Append("<nav epub:type=\"toc\" xmlns:epub=\"http://www.idpf.org/2007/ops\">\n<h1>Contents</h1>\n<ol>\n");
        sb.Append("<li><a href=\"title.xhtml\">Title Page</a></li>\n");
        for (int i = 0; i < story.Chapters.Count; i++)
        {
            var chapter = story.Chapters[i];
            sb.Append($"<li><a href=\"chapter-{i + 1}.xhtml\">Chapter {chapter.ChapterNumber}: {XmlEscape(chapter.Title)}</a></li>\n");
        }
        sb.Append("</ol>\n</nav>\n</body>\n</html>\n");
        return sb.ToString();
    }

    private static string BuildTitlePage(StoryExportModel story)
    {
        var sb = new StringBuilder();
        sb.Append(XhtmlHead(story.Title));
        sb.Append($"<h1>{XmlEscape(story.Title)}</h1>\n");
        sb.Append($"<p>by {XmlEscape(story.AuthorName)}</p>\n");
        sb.Append($"<p>Rated {XmlEscape(story.RatingLabel)}</p>\n");
        if (!string.IsNullOrWhiteSpace(story.LongDescriptionHtml))
        {
            sb.Append(ToXhtml(story.LongDescriptionHtml));
        }
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    private static string BuildChapterPage(Core.ChapterExportDto chapter)
    {
        var sb = new StringBuilder();
        sb.Append(XhtmlHead($"Chapter {chapter.ChapterNumber}: {chapter.Title}"));
        sb.Append($"<h1>Chapter {chapter.ChapterNumber}: {XmlEscape(chapter.Title)}</h1>\n");
        if (!string.IsNullOrWhiteSpace(chapter.TopAuthorsNote))
        {
            sb.Append("<aside><p><em>Author's note:</em></p>\n").Append(ToXhtml(chapter.TopAuthorsNote)).Append("</aside>\n");
        }
        sb.Append(ToXhtml(chapter.HtmlContent));
        if (!string.IsNullOrWhiteSpace(chapter.BottomAuthorsNote))
        {
            sb.Append("<aside><p><em>Author's note:</em></p>\n").Append(ToXhtml(chapter.BottomAuthorsNote)).Append("</aside>\n");
        }
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    private static string XhtmlHead(string title) =>
        $"""
         <?xml version="1.0" encoding="UTF-8"?>
         <!DOCTYPE html>
         <html xmlns="http://www.w3.org/1999/xhtml">
         <head><title>{XmlEscape(title)}</title></head>
         <body>

         """;

    /// <summary>
    /// Re-serializes sanitized HTML as well-formed XHTML (self-closed <c>&lt;br/&gt;</c>, all end
    /// tags present) — the stored form is HTML5-lax and would make the content document invalid XML.
    /// </summary>
    private static string ToXhtml(string html)
    {
        var sb = new StringBuilder();
        foreach (INode node in ExportDom.ParseFragment(html))
        {
            sb.Append(node.ToHtml(XhtmlMarkupFormatter.Instance));
        }
        sb.Append('\n');
        return sb.ToString();
    }

    private static string XmlEscape(string text) =>
        WebUtility.HtmlEncode(text);
}
