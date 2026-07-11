using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for the chapter import pipeline (Feature 63, WU38d): the normalizer, the splitter,
/// the service guards, and — the backbone — <b>round-trips through the WU38c export writers</b>
/// (export a story → import the bytes → the allowlist content survives). Tier: Unit
/// (directly-constructed Server services, no host/DB — testing.md).
/// </summary>
public class ContentImportTests
{
    private static ServerContentImportService Service() =>
        new(new ServerHtmlSanitizationService(), NullLogger<ServerContentImportService>.Instance);

    /// <summary>Allowlist-rich chapter body WITHOUT h2/h3 (heading demotion tested separately).</summary>
    private const string RichBody =
        "<p>Plain and <strong>bold</strong> and <em>italic</em> and <u>underlined</u> and <s>struck</s>.</p>" +
        "<blockquote>Quoted words.</blockquote>" +
        "<ul><li>Apple</li><li>Berry</li></ul>" +
        "<ol><li>First</li><li>Second</li></ol>" +
        "<p>See <a href=\"https://example.com/fic\">this fic</a>.</p>";

    private static StoryExportModel ExportFixture() => new(
        StoryId: 42,
        Title: "Tin & Flute",
        AuthorName: "AuthorX",
        Rating: Rating.T,
        LongDescriptionHtml: "<p>A story about things.</p>",
        PublishDate: new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
        LastUpdatedDate: new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc),
        Chapters:
        [
            new ChapterExportDto(1, "The Harbor", RichBody, null, null),
            new ChapterExportDto(2, "The Lighthouse", "<p>Second chapter text.</p>", null, null)
        ]);

    private static void AssertRichContentSurvived(string html)
    {
        html.Should().Contain("<strong>bold</strong>")
            .And.Contain("<em>italic</em>")
            .And.Contain("<s>struck</s>")
            .And.Contain("Quoted words.")
            .And.Contain("<li>Apple</li>")
            .And.Contain("<li>First</li>")
            .And.Contain("https://example.com/fic");
    }

    // ── Round-trips through the export writers ───────────────────────────────────

    [Fact]
    public async Task RoundTrip_Docx_WholeStory_SplitsAtChapterHeadings_WithContentPreserved()
    {
        byte[] docx = DocxWriter.Write(ExportFixture());

        ImportParseResult result = await Service()
            .ParseDocumentAsync(new MemoryStream(docx), "story.docx", ImportFormat.Docx);

        // Export maps chapter titles → "Heading 1"; import maps Heading 1 → h2 → TopHeading split.
        result.SuggestedStrategy.Should().Be(SplitStrategy.TopHeading);

        // Title-page block (story title is "Title"-styled → h2 boundary too) + 2 chapters.
        result.Drafts.Should().HaveCount(3);
        result.Drafts[0].Title.Should().Be("Tin & Flute", "the title page is a droppable front-matter draft");
        result.Drafts[1].Title.Should().Be("Chapter 1: The Harbor");
        result.Drafts[2].Title.Should().Be("Chapter 2: The Lighthouse");

        AssertRichContentSurvived(result.Drafts[1].Html);
        result.Drafts[1].Html.Should().Contain("<u>underlined</u>", "the u => u style map keeps underline");
        result.Drafts[2].Html.Should().Contain("Second chapter text.");
    }

    [Fact]
    public async Task RoundTrip_Docx_PageBreakStrategy_IsAvailable()
    {
        // DocxWriter emits a page break before each chapter — Mammoth's br[type='page'] => hr map
        // must surface them as split markers (this is the "verify at build" check from the plan).
        byte[] docx = DocxWriter.Write(ExportFixture());

        ImportParseResult result = await Service()
            .ParseDocumentAsync(new MemoryStream(docx), "story.docx", ImportFormat.Docx);

        result.AvailableStrategies.Should().Contain(SplitStrategy.PageBreak);
        IReadOnlyList<ImportedChapterDraft> byPageBreak =
            Service().Resplit(result, SplitStrategy.PageBreak);
        byPageBreak.Should().HaveCount(3, "title page + one page-break-opened segment per chapter");
    }

    [Fact]
    public async Task RoundTrip_Docx_SingleChapter_ParseSingle_NeverSplits()
    {
        byte[] docx = DocxWriter.Write(ExportFixture());

        ImportedChapterDraft draft = await Service()
            .ParseSingleAsync(new MemoryStream(docx), "story.docx", ImportFormat.Docx);

        // Mode 1/3 contract: one draft, no matter how many headings the doc has.
        AssertRichContentSurvived(draft.Html);
        draft.Html.Should().Contain("Second chapter text.");
        draft.WordCount.Should().BeGreaterThan(20);
    }

    [Fact]
    public async Task RoundTrip_Html_SplitsAtChapterHeadings_WithExactAllowlistMarkup()
    {
        byte[] html = HtmlWriter.Write(ExportFixture());

        ImportParseResult result = await Service()
            .ParseDocumentAsync(new MemoryStream(html), "story.html", ImportFormat.Html);

        result.SuggestedStrategy.Should().Be(SplitStrategy.TopHeading);
        ImportedChapterDraft harbor = result.Drafts.Single(d => d.Title == "Chapter 1: The Harbor");
        AssertRichContentSurvived(harbor.Html);
        harbor.Html.Should().Contain("<u>underlined</u>");
    }

    [Fact]
    public async Task RoundTrip_Markdown_RebuildsAllowlistHtml()
    {
        byte[] md = MarkdownWriter.Write(ExportFixture());

        ImportParseResult result = await Service()
            .ParseDocumentAsync(new MemoryStream(md), "story.md", ImportFormat.Markdown);

        result.SuggestedStrategy.Should().Be(SplitStrategy.TopHeading);
        ImportedChapterDraft harbor = result.Drafts.Single(d => d.Title == "Chapter 1: The Harbor");
        harbor.Html.Should().Contain("<strong>bold</strong>")
            .And.Contain("<em>italic</em>")
            .And.Contain("Quoted words.")
            .And.Contain("<li>Apple</li>")
            .And.Contain("https://example.com/fic");
        // Documented lossy corner: u has no Markdown form — text survives, tag doesn't.
        harbor.Html.Should().Contain("underlined").And.NotContain("<u>");
    }

    [Fact]
    public async Task RoundTrip_Txt_ParseSingle_ProducesParagraphs()
    {
        byte[] txt = TxtWriter.Write(ExportFixture());

        ImportedChapterDraft draft = await Service()
            .ParseSingleAsync(new MemoryStream(txt), "story.txt", ImportFormat.Txt);

        draft.Html.Should().StartWith("<p>").And.Contain("Quoted words.").And.Contain("Second chapter text.");
        draft.WordCount.Should().BeGreaterThan(20);
    }

    [Fact]
    public async Task RoundTrip_Epub_SpineDefinesChapters_NavDefinesTitles()
    {
        byte[] epub = EpubWriter.Write(ExportFixture());

        ImportParseResult result = await Service().ParseEpubAsync(new MemoryStream(epub));

        result.BookTitle.Should().Be("Tin & Flute");
        result.BookAuthor.Should().Be("AuthorX");
        result.AvailableStrategies.Should().BeEmpty("EPUB chapters are spine-defined — no delimiter picker");

        // Title page + 2 chapters (title page has text → kept, droppable in review).
        result.Drafts.Should().HaveCount(3);
        ImportedChapterDraft harbor = result.Drafts.Single(d => d.Title == "Chapter 1: The Harbor");
        AssertRichContentSurvived(harbor.Html);
    }

    // ── Splitter ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Splitter_TopHeading_ConsumesBoundary_AndKeepsFrontMatter()
    {
        const string html = "<p>Front matter.</p><h2>One</h2><p>a</p><h2>Two</h2><p>b</p>";

        IReadOnlyList<ChapterSplitter.Segment> segments =
            ChapterSplitter.Split(html, SplitStrategy.TopHeading);

        segments.Should().HaveCount(3);
        segments[0].Title.Should().BeNull("front matter has no boundary title");
        segments[0].Html.Should().Contain("Front matter.");
        segments[1].Title.Should().Be("One");
        segments[1].Html.Should().Contain("<p>a</p>").And.NotContain("<h2>", "the boundary heading is consumed");
        segments[2].Title.Should().Be("Two");
    }

    [Fact]
    public void Splitter_ChapterTextPattern_MatchesShortChapterLines_NotProse()
    {
        const string html =
            "<p>Chapter 1</p><p>Body one.</p>" +
            "<p>The chapter 2 of his life was long — this prose sentence must not split anything even though it mentions chapters.</p>" +
            "<p>Chapter 2: The Return</p><p>Body two.</p>";

        IReadOnlyList<ChapterSplitter.Segment> segments =
            ChapterSplitter.Split(html, SplitStrategy.ChapterTextPattern);

        segments.Should().HaveCount(2);
        segments[0].Title.Should().Be("Chapter 1");
        segments[0].Html.Should().Contain("prose sentence", "mid-prose mentions must not split");
        segments[1].Title.Should().Be("Chapter 2: The Return");
    }

    [Fact]
    public void Splitter_None_ReturnsWholeDocumentAsOneSegment()
    {
        ChapterSplitter.Split("<h2>One</h2><p>a</p>", SplitStrategy.None)
            .Should().HaveCount(1);
    }

    [Fact]
    public void Splitter_Suggest_PrefersTopHeading_ThenFallsBack()
    {
        (SplitStrategy suggested, IReadOnlyList<SplitStrategy> available) =
            ChapterSplitter.Suggest("<h2>A</h2><p>x</p><h2>B</h2><p>y</p>");
        suggested.Should().Be(SplitStrategy.TopHeading);
        available.Should().Contain(SplitStrategy.None);

        (suggested, _) = ChapterSplitter.Suggest("<h3>A</h3><p>x</p><h3>B</h3><p>y</p>");
        suggested.Should().Be(SplitStrategy.SubHeading);

        (suggested, available) = ChapterSplitter.Suggest("<p>Just one chapter of prose.</p>");
        suggested.Should().Be(SplitStrategy.None);
        available.Should().Equal(SplitStrategy.None);
    }

    // ── Normalizer ───────────────────────────────────────────────────────────────

    [Fact]
    public void Normalizer_MapsLegacyAndDeepMarkup_TowardTheAllowlist()
    {
        ImportHtmlNormalizer.Result result = ImportHtmlNormalizer.Normalize(
            "<h1>Top</h1><h4>Deep</h4><p><b>b</b> and <i>i</i> and <span>plain</span></p>" +
            "<div>Loose text</div><div><p>Wrapped</p></div>");

        result.Html.Should().Contain("<h2>Top</h2>", "h1 demotes to h2");
        result.Html.Should().Contain("<h3>Deep</h3>", "h4+ demotes to h3");
        result.Html.Should().Contain("<strong>b</strong>").And.Contain("<em>i</em>");
        result.Html.Should().Contain("plain").And.NotContain("<span");
        result.Html.Should().Contain("<p>Loose text</p>", "inline-only containers wrap in p");
        result.Html.Should().Contain("<p>Wrapped</p>").And.NotContain("<div", "containers unwrap");
        result.Warnings.Should().Contain(w => w.Kind == ImportWarningKind.UnsupportedFormatting);
    }

    [Fact]
    public void Normalizer_DropsImagesWithWarning_AndScriptsSilently()
    {
        ImportHtmlNormalizer.Result result = ImportHtmlNormalizer.Normalize(
            "<p>Before</p><img src=\"data:image/png;base64,xx\"><script>alert(1)</script><p>After</p>");

        result.Html.Should().Contain("Before").And.Contain("After")
            .And.NotContain("<img").And.NotContain("alert(1)");
        result.Warnings.Should().ContainSingle(w => w.Kind == ImportWarningKind.ImagesDropped)
            .Which.Message.Should().Contain("1 image");
    }

    [Fact]
    public void Normalizer_FlattensTables_WithWarning()
    {
        ImportHtmlNormalizer.Result result = ImportHtmlNormalizer.Normalize(
            "<table><tr><td>A</td><td>B</td></tr><tr><td>C</td></tr></table>");

        // WebUtility.HtmlEncode writes the middot as a numeric entity.
        result.Html.Should().Contain("<p>A &#183; B</p>").And.Contain("<p>C</p>").And.NotContain("<table");
        result.Warnings.Should().Contain(w => w.Kind == ImportWarningKind.UnsupportedFormatting);
    }

    // ── Guards ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ParseSingleAsync_EpubFormat_IsRejected_UseEpubPath()
    {
        Func<Task> act = () => Service()
            .ParseSingleAsync(new MemoryStream([1, 2, 3]), "book.epub", ImportFormat.Epub);

        await act.Should().ThrowAsync<ImportException>();
    }

    [Fact]
    public async Task ParseSingleAsync_NonZipBytesAsDocx_ThrowsFriendlyError()
    {
        byte[] notAZip = Encoding.UTF8.GetBytes("This is not a zip file at all.");

        Func<Task> act = () => Service()
            .ParseSingleAsync(new MemoryStream(notAZip), "fake.docx", ImportFormat.Docx);

        (await act.Should().ThrowAsync<ImportException>())
            .Which.Message.Should().Contain("Google Docs", "the message teaches the Google Docs export path");
    }

    [Fact]
    public async Task ParseSingleAsync_OversizedFile_ThrowsFriendlyError()
    {
        using var oversized = new MemoryStream(new byte[ImportLimits.MaxFileBytes + 1]);

        Func<Task> act = () => Service()
            .ParseSingleAsync(oversized, "big.txt", ImportFormat.Txt);

        (await act.Should().ThrowAsync<ImportException>()).Which.Message.Should().Contain("MB");
    }

    [Fact]
    public async Task ImportedHtml_IsSanitized_EvenWhenSourceSmugglesMarkup()
    {
        // A hostile HTML upload: script + event handler + javascript: href.
        byte[] hostile = Encoding.UTF8.GetBytes(
            "<html><body><p onclick=\"steal()\">Text</p><script>steal()</script>" +
            "<p><a href=\"javascript:steal()\">link</a></p></body></html>");

        ImportedChapterDraft draft = await Service()
            .ParseSingleAsync(new MemoryStream(hostile), "evil.html", ImportFormat.Html);

        draft.Html.Should().Contain("Text").And.Contain("link");
        draft.Html.Should().NotContain("steal").And.NotContain("onclick").And.NotContain("javascript:");
    }
}
