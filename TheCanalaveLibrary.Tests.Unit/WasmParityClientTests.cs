using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using TheCanalaveLibrary.Client;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Service audit §2.7's client-side parity breaks (WU-ParityAndRemaining, 2026-10-13), each pinned on
/// the client class over a canned handler (no host):
/// <list type="bullet">
/// <item>§2.7.4 — the import re-split's read used case-sensitive JSON defaults, so the
/// server's camelCase <c>title</c>/<c>html</c> never bound and every draft came back blank on WASM.</item>
/// <item>§2.7.7 — the Explore user pivot's 400 lost its message to a bare
/// <c>EnsureSuccessStatusCode</c>.</item>
/// <item>§2.7.3 — the WASM host's copy of the server's sprite root (<see cref="ClientRuntimeConfig"/>):
/// adopted on success, the default kept and a Warning logged on any failure, never a throw.</item>
/// </list>
/// Tier: Unit.
/// </summary>
public class WasmParityClientTests
{
    // ── §2.7.4: the re-split reads camelCase ─────────────────────────────────────

    [Fact]
    public async Task Resplit_BindsTheServersCamelCaseDrafts()
    {
        CannedHandler handler = new(HttpStatusCode.OK,
            """[{"title":"Chapter One","html":"<p>First.</p>","wordCount":1,"warnings":[{"kind":2,"message":"Flattened a table."}]},{"title":null,"html":"<p>Second.</p>","wordCount":1,"warnings":[]}]""",
            "application/json");
        ClientContentImportService svc = new(NewClient(handler));

        IReadOnlyList<ImportedChapterDraft> drafts = await svc.ResplitAsync(Parsed(), SplitStrategy.TopHeading);

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/content-import/resplit");
        drafts.Should().HaveCount(2);
        drafts[0].Title.Should().Be("Chapter One");
        drafts[0].Html.Should().Be("<p>First.</p>");
        drafts[0].Warnings.Should().ContainSingle()
            .Which.Should().Be(new ImportWarning(ImportWarningKind.UnsupportedFormatting, "Flattened a table."));
        drafts[1].Html.Should().Be("<p>Second.</p>");
    }

    [Fact]
    public async Task Resplit_A400_ThrowsImportException_WithTheServersDetail()
    {
        ClientContentImportService svc = new(NewClient(new CannedHandler(HttpStatusCode.BadRequest,
            """{"status":400,"detail":"That split would make more than 500 chapters."}""")));

        Func<Task> act = () => svc.ResplitAsync(Parsed(), SplitStrategy.SubHeading);

        await act.Should().ThrowAsync<ImportException>().WithMessage("That split would make more than 500 chapters.");
    }

    [Fact]
    public async Task Resplit_A401_KeepsTheBusinessMessage()
    {
        // The documented deviation: Resplit's own InvalidOperationException is (mis)mapped to 401 by the
        // server, and the client preserves the real message — which it could not read before.
        ClientContentImportService svc = new(NewClient(new CannedHandler(HttpStatusCode.Unauthorized,
            """{"status":401,"detail":"Re-split requires a parsed document; EPUB chapters are spine-defined."}""")));

        Func<Task> act = () => svc.ResplitAsync(Parsed(), SplitStrategy.SubHeading);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Re-split requires a parsed document*");
    }

    // ── §2.7.7: the Explore user pivot keeps its message ─────────────────────────

    [Fact]
    public async Task GetUserNeighbors_A400_RebuildsTheStoryValidationException()
    {
        ClientManualTreeSearchReadService svc = new(NewClient(new CannedHandler(HttpStatusCode.BadRequest,
            """{"status":400,"detail":"Story validation failed.","errors":["A ship can have at most 4 members."]}""")));

        Func<Task> act = () => svc.GetUserNeighborsAsync(new UserNeighborsRequest { UserId = 7 });

        (await act.Should().ThrowAsync<StoryValidationException>())
            .Which.Errors.Should().Equal("A ship can have at most 4 members.");
    }

    // ── §2.7.3: ClientRuntimeConfig ──────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_A200_AdoptsTheServersSpriteRoot()
    {
        CannedHandler handler = new(HttpStatusCode.OK,
            """{"spriteBaseUrl":"https://cdn.example/sprites"}""", "application/json");
        ClientRuntimeConfig config = new();
        ListLogger logger = new();

        await config.LoadAsync(NewClient(handler), logger);

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/client-config");
        config.SpriteBaseUrl.Should().Be("https://cdn.example/sprites");
        logger.Entries.Should().BeEmpty();
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, """{"status":500}""")]
    [InlineData(HttpStatusCode.OK, "not json")]
    [InlineData(HttpStatusCode.OK, """{"spriteBaseUrl":""}""")]
    public async Task LoadAsync_AFailure_KeepsTheDefault_AndLogsAWarning(HttpStatusCode status, string body)
    {
        ClientRuntimeConfig config = new();
        ListLogger logger = new();

        await config.LoadAsync(NewClient(new CannedHandler(status, body, "application/json")), logger);

        config.SpriteBaseUrl.Should().Be(ClientConfigDto.DefaultSpriteBaseUrl);
        logger.Entries.Should().ContainSingle().Which.Level.Should().Be(LogLevel.Warning,
            "a failed load is observed, never silent (logging.md)");
    }

    [Fact]
    public async Task LoadAsync_ANetworkFailure_KeepsTheDefault_AndNeverThrows()
    {
        ClientRuntimeConfig config = new();
        ListLogger logger = new();

        await FluentActions.Awaiting(() => config.LoadAsync(NewClient(new ThrowingHandler()), logger))
            .Should().NotThrowAsync("a config fetch must never block WASM boot");
        config.SpriteBaseUrl.Should().Be("/sprites/themes");
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning && e.Exception is HttpRequestException);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static ImportParseResult Parsed() => new(
        NormalizedHtml: "<h2>Chapter One</h2><p>First.</p><p>Second.</p>",
        SuggestedStrategy: SplitStrategy.TopHeading,
        AvailableStrategies: [SplitStrategy.TopHeading, SplitStrategy.SubHeading],
        Drafts: [],
        Warnings: []);

    private static HttpClient NewClient(HttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("https://localhost/") };

    private sealed class CannedHandler(HttpStatusCode status, string body, string mediaType = "application/problem+json")
        : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, mediaType)
            });
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("connection refused");
    }

    private sealed class ListLogger : ILogger
    {
        public List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, exception));
    }
}
