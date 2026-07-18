using System.IO.Compression;
using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="IExportService"/> + the export endpoint (Feature 54, WU38c).
/// Covers: per-format generation against real seeded data, "export = what you can read"
/// (content-rating gate via the fake <see cref="IActiveUserContext"/>), unpublished-chapter
/// exclusion, chapter ordering, and the HTTP surface (200 + Content-Disposition: attachment,
/// 404 on bad format/missing story). Tier: Integration (real Testcontainers Postgres — the
/// chapters-for-export projection must translate to SQL).
/// </summary>
[Collection("Postgres")]
public class ExportServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _authorId;
    private int _storyId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        // Chapter writes gate on story authorship (MA-301) — the fixture story needs a real
        // author, and chapter seeding runs as them (SeedPublishedChapterAsync handles the swap).
        _authorId = await SeedUserAsync("export-author");
        _storyId = await SeedStoryAsync(_authorId);
        await SeedPublishedChapterAsync(_storyId, "The Harbor",
            "<p>Plain and <strong>bold</strong> text.</p><blockquote>Quoted words.</blockquote>");
        await SeedPublishedChapterAsync(_storyId, "The Lighthouse",
            "<p>Second chapter text.</p>");
    }

    /// <summary>
    /// Creates a chapter through the real write service (server-assigned number, sanitize-on-save,
    /// word count) and publishes it — export only carries published chapters. Rating is left null
    /// (inherit the story's) — a primary version's rating must match the story's, so an explicit
    /// value would throw on non-E stories. Runs as the story's author (chapter writes gate on
    /// authorship; mature-enabled so M-rated fixture stories resolve through the rating filter) and
    /// restores the anonymous viewer before returning.
    /// </summary>
    private async Task<int> SeedPublishedChapterAsync(int storyId, string title, string html)
    {
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorId, showMatureContent: true));

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IChapterWriteService write = scope.ServiceProvider.GetRequiredService<IChapterWriteService>();
        int chapterId = await write.CreateChapterAsync(new CreateChapterDto
        {
            StoryId = storyId,
            Title = title,
            ChapterText = html,
            Rating = null
        });
        await write.SetPublishedAsync(chapterId, true);

        SetActiveUser(FakeActiveUserContext.Anonymous());
        return chapterId;
    }

    private async Task<StoryExportResult?> ExportAsync(int storyId, ExportFormat format)
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IExportService export = scope.ServiceProvider.GetRequiredService<IExportService>();
        return await export.ExportStoryAsync(storyId, format);
    }

    // ── Per-format generation ────────────────────────────────────────────────────

    [Theory]
    [InlineData(ExportFormat.Epub, "application/epub+zip", ".epub")]
    [InlineData(ExportFormat.Pdf, "application/pdf", ".pdf")]
    [InlineData(ExportFormat.Html, "text/html", ".html")]
    [InlineData(ExportFormat.Txt, "text/plain", ".txt")]
    [InlineData(ExportFormat.Markdown, "text/markdown", ".md")]
    [InlineData(ExportFormat.Docx,
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx")]
    public async Task ExportStoryAsync_EachFormat_ReturnsBytesContentTypeAndSluggedFileName(
        ExportFormat format, string expectedContentType, string expectedExtension)
    {
        StoryExportResult? result = await ExportAsync(_storyId, format);

        result.Should().NotBeNull();
        result!.Content.Length.Should().BeGreaterThan(0);
        result.ContentType.Should().Be(expectedContentType);
        result.FileName.Should().EndWith(expectedExtension)
            .And.StartWith("test-story-", "the filename is the slugified story title");
    }

    [Fact]
    public async Task ExportStoryAsync_Epub_IsValidZip_WithBothChaptersInSpineOrder()
    {
        StoryExportResult? result = await ExportAsync(_storyId, ExportFormat.Epub);

        using var zip = new ZipArchive(new MemoryStream(result!.Content), ZipArchiveMode.Read);
        zip.Entries.Select(e => e.FullName).Should()
            .Contain(["mimetype", "OEBPS/content.opf", "OEBPS/chapter-1.xhtml", "OEBPS/chapter-2.xhtml"]);

        using var reader1 = new StreamReader(zip.GetEntry("OEBPS/chapter-1.xhtml")!.Open());
        reader1.ReadToEnd().Should().Contain("Quoted words.");
        using var reader2 = new StreamReader(zip.GetEntry("OEBPS/chapter-2.xhtml")!.Open());
        reader2.ReadToEnd().Should().Contain("Second chapter text.");
    }

    [Fact]
    public async Task ExportStoryAsync_Txt_ChaptersAppearInChapterNumberOrder()
    {
        StoryExportResult? result = await ExportAsync(_storyId, ExportFormat.Txt);
        string txt = Encoding.UTF8.GetString(result!.Content);

        int first = txt.IndexOf("The Harbor", StringComparison.Ordinal);
        int second = txt.IndexOf("The Lighthouse", StringComparison.Ordinal);
        first.Should().BeGreaterThan(-1);
        second.Should().BeGreaterThan(first, "chapter 1 must precede chapter 2");
    }

    // ── Permission model: "export = what you can read" ──────────────────────────

    [Fact]
    public async Task ExportStoryAsync_MissingStory_ReturnsNull()
    {
        StoryExportResult? result = await ExportAsync(999_999_999, ExportFormat.Epub);
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExportStoryAsync_MatureStory_GatedByViewersContentRatingCeiling()
    {
        int viewerId = await SeedUserAsync();
        int matureStoryId = await SeedStoryAsync(_authorId, rating: Rating.M);
        await SeedPublishedChapterAsync(matureStoryId, "Mature Ch", "<p>mature body</p>");

        // Non-mature viewer: the content-rating master filter hides the story entirely.
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(viewerId, showMatureContent: false));
        (await ExportAsync(matureStoryId, ExportFormat.Txt))
            .Should().BeNull("export must never exceed what reading shows");

        // Mature-enabled viewer: full export.
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(viewerId, showMatureContent: true));
        StoryExportResult? allowed = await ExportAsync(matureStoryId, ExportFormat.Txt);
        allowed.Should().NotBeNull();
        Encoding.UTF8.GetString(allowed!.Content).Should().Contain("mature body");
    }

    [Fact]
    public async Task ExportStoryAsync_ExcludesUnpublishedChapters()
    {
        // A third, unpublished chapter (created but never SetPublished) — created as the author
        // (chapter writes gate on story authorship), then export back to the anonymous viewer.
        SetActiveUser(_authorId);
        await using (AsyncServiceScope scope = Factory.Services.CreateAsyncScope())
        {
            IChapterWriteService write = scope.ServiceProvider.GetRequiredService<IChapterWriteService>();
            await write.CreateChapterAsync(new CreateChapterDto
            {
                StoryId = _storyId,
                Title = "Secret Draft",
                ChapterText = "<p>unfinished draft text</p>",
                Rating = Rating.E
            });
        }
        SetActiveUser(FakeActiveUserContext.Anonymous());

        StoryExportResult? result = await ExportAsync(_storyId, ExportFormat.Txt);
        string txt = Encoding.UTF8.GetString(result!.Content);

        txt.Should().Contain("Second chapter text.");
        txt.Should().NotContain("unfinished draft text", "drafts never leave the site");
    }

    // ── HTTP surface ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExportEndpoint_Get_Returns200_WithAttachmentDisposition()
    {
        using HttpClient client = Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync($"/api/stories/{_storyId}/export/epub");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/epub+zip");
        response.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
        response.Content.Headers.ContentDisposition.FileName.Should().Contain("test-story-");
        (await response.Content.ReadAsByteArrayAsync()).Length.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("mobi")]     // deliberately unsupported format
    [InlineData("3")]        // numeric enum value — alpha-only guard rejects
    [InlineData("exe")]
    public async Task ExportEndpoint_UnknownFormat_Returns404(string format)
    {
        using HttpClient client = Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync($"/api/stories/{_storyId}/export/{format}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ExportEndpoint_MissingStory_Returns404()
    {
        using HttpClient client = Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/stories/999999999/export/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
