using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for the six export writers (WU38c) — pure functions over
/// <see cref="StoryExportModel"/>, directly constructed (no host/DB). The fixture chapter covers
/// the full 13-tag sanitizer allowlist so every writer's mapping is exercised
/// (layer2-services.md §"Export &amp; Import — the Allowlist Is the Interchange Contract").
/// </summary>
public class ExportWritersTests
{
    // "&" in the title exercises XML/HTML escaping in every structured writer.
    private static StoryExportModel Fixture() => new(
        StoryId: 42,
        Title: "Tin & Flute",
        AuthorName: "AuthorX",
        Rating: Rating.T,
        LongDescriptionHtml: "<p>A story about <strong>things</strong>.</p>",
        PublishDate: new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
        LastUpdatedDate: new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc),
        Chapters:
        [
            new ChapterExportDto(
                ChapterNumber: 1,
                Title: "The Harbor",
                HtmlContent:
                    "<p>Plain and <strong>bold</strong> and <em>italic</em> and <u>underlined</u> and <s>struck</s>.</p>" +
                    "<h2>Scene Two</h2>" +
                    "<p>Line one<br>line two.</p>" +
                    "<blockquote>Quoted words.</blockquote>" +
                    "<ul><li>Apple</li><li>Berry</li></ul>" +
                    "<ol><li>First</li><li>Second</li></ol>" +
                    "<p>See <a href=\"https://example.com/fic\">this fic</a>.</p>" +
                    "<h3>Aside</h3>",
                TopAuthorsNote: "<p>Thanks for reading!</p>",
                BottomAuthorsNote: null),
            new ChapterExportDto(
                ChapterNumber: 2,
                Title: "The Lighthouse",
                HtmlContent: "<p>Second chapter text.</p>",
                TopAuthorsNote: null,
                BottomAuthorsNote: "<p>See you next week.</p>")
        ]);

    // ── EPUB ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Epub_MimetypeIsFirstEntry_AndStoredUncompressed()
    {
        byte[] epub = EpubWriter.Write(Fixture());

        // OCF requirement: readers sniff "mimetype" at byte offset 30 (fixed local-header size),
        // which only holds when the entry is first AND stored.
        Encoding.ASCII.GetString(epub, 30, 8).Should().Be("mimetype");
        Encoding.ASCII.GetString(epub, 38, 20).Should().Be("application/epub+zip");
    }

    [Fact]
    public void Epub_ContainsPackageNavAndOneXhtmlPerChapter()
    {
        byte[] epub = EpubWriter.Write(Fixture());

        using var zip = new ZipArchive(new MemoryStream(epub), ZipArchiveMode.Read);
        string[] names = zip.Entries.Select(e => e.FullName).ToArray();
        names.Should().Contain(["mimetype", "META-INF/container.xml", "OEBPS/content.opf",
            "OEBPS/nav.xhtml", "OEBPS/title.xhtml", "OEBPS/chapter-1.xhtml", "OEBPS/chapter-2.xhtml"]);

        string opf = ReadEntry(zip, "OEBPS/content.opf");
        opf.Should().Contain("Tin &amp; Flute").And.Contain("AuthorX")
           .And.Contain("urn:canalave:story:42");
    }

    [Fact]
    public void Epub_ChapterDocuments_AreWellFormedXhtml_WithContentPreserved()
    {
        byte[] epub = EpubWriter.Write(Fixture());

        using var zip = new ZipArchive(new MemoryStream(epub), ZipArchiveMode.Read);
        string chapter1 = ReadEntry(zip, "OEBPS/chapter-1.xhtml");

        // Well-formed XML — this is exactly what bare stored "<br>" would break.
        Action parse = () => XDocument.Parse(chapter1);
        parse.Should().NotThrow("EPUB content documents must be well-formed XHTML");

        chapter1.Should().Contain("<strong>bold</strong>")
            .And.Contain("Quoted words.")
            .And.Contain("https://example.com/fic")
            .And.Contain("Thanks for reading!");
        ReadEntry(zip, "OEBPS/chapter-2.xhtml").Should().Contain("See you next week.");
    }

    // ── PDF ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Pdf_HasMagicBytes_AndNonTrivialSize()
    {
        byte[] pdf = PdfWriter.Write(Fixture());

        Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
        pdf.Length.Should().BeGreaterThan(1_000, "two chapters of content should produce a real document");
    }

    // ── DOCX ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Docx_Opens_WithChapterHeadingsBodyTextAndHeadingStyles()
    {
        byte[] docx = DocxWriter.Write(Fixture());

        using var word = WordprocessingDocument.Open(new MemoryStream(docx), isEditable: false);
        string bodyText = word.MainDocumentPart!.Document.Body!.InnerText;

        bodyText.Should().Contain("Tin & Flute")
            .And.Contain("Chapter 1: The Harbor")
            .And.Contain("bold")
            .And.Contain("Quoted words.")
            .And.Contain("Second chapter text.");

        // Real named heading styles — Word's nav pane and Mammoth-based re-import both key off these.
        string stylesXml = word.MainDocumentPart!.StyleDefinitionsPart!.Styles!.OuterXml;
        stylesXml.Should().Contain("Heading1").And.Contain("Heading2").And.Contain("Title");

        // The <a href> became a real external hyperlink relationship.
        word.MainDocumentPart!.HyperlinkRelationships
            .Should().Contain(r => r.Uri.ToString() == "https://example.com/fic");
    }

    // ── HTML ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Html_EscapesMetadata_AndEmbedsChapterMarkupVerbatim()
    {
        string html = Encoding.UTF8.GetString(HtmlWriter.Write(Fixture()));

        html.Should().Contain("<title>Tin &amp; Flute</title>", "composed metadata is encoded");
        html.Should().Contain("Chapter 1: The Harbor").And.Contain("Chapter 2: The Lighthouse");
        // Stored chapter HTML is already-sanitized trusted markup — embedded verbatim.
        html.Should().Contain("<strong>bold</strong>").And.Contain("<blockquote>Quoted words.</blockquote>");
    }

    // ── TXT ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Txt_StripsAllMarkup_ButKeepsDocumentShape()
    {
        string txt = Encoding.UTF8.GetString(TxtWriter.Write(Fixture()));

        txt.Should().NotContain("<strong>").And.NotContain("<p>");
        txt.Should().Contain("Tin & Flute").And.Contain("by AuthorX");
        txt.Should().Contain("Chapter 1: The Harbor");
        txt.Should().Contain("Plain and bold and italic and underlined and struck.");
        txt.Should().Contain("Line one\nline two.", "br becomes a newline");
        txt.Should().Contain("> Quoted words.");
        txt.Should().Contain("- Apple").And.Contain("- Berry");
        txt.Should().Contain("1. First").And.Contain("2. Second");
    }

    // ── Markdown ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Markdown_MapsEveryAllowlistTag()
    {
        string md = Encoding.UTF8.GetString(MarkdownWriter.Write(Fixture()));

        md.Should().Contain("# Tin & Flute");
        md.Should().Contain("## Chapter 1: The Harbor");
        md.Should().Contain("**bold**").And.Contain("*italic*").And.Contain("~~struck~~");
        // u has no Markdown equivalent — text preserved, no tag leaks through.
        md.Should().Contain("underlined").And.NotContain("<u>");
        md.Should().Contain("## Scene Two").And.Contain("### Aside");
        md.Should().Contain("> Quoted words.");
        md.Should().Contain("- Apple").And.Contain("1. First").And.Contain("2. Second");
        md.Should().Contain("[this fic](https://example.com/fic)");
    }

    [Fact]
    public void Markdown_BrBecomesHardBreak()
    {
        string md = Encoding.UTF8.GetString(MarkdownWriter.Write(Fixture()));

        md.Should().Contain("Line one  \nline two.", "Markdown hard break is two trailing spaces + newline");
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    private static string ReadEntry(ZipArchive zip, string name)
    {
        ZipArchiveEntry entry = zip.GetEntry(name) ?? throw new InvalidOperationException($"Missing entry {name}");
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
