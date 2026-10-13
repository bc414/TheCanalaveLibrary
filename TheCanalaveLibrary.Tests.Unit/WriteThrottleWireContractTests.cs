using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Client;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// The service throttle's 429 across the HTTP hop (WU-ThrottleCoverage, 2026-10-13; security.md
/// §"Write Throttling", layer5-wasm.md §"The Error-Translation Contract"). The server half:
/// <see cref="EndpointHelpers.ExecuteAsync"/> puts <c>retryAfterSeconds</c> and <c>writeActionKind</c> in
/// the 429's <c>ProblemDetails</c>. The client half: <c>ClientHttpHelpers.ThrowIfWriteFailedAsync</c>
/// rebuilds <see cref="WriteRateLimitExceededException"/> from that body for EVERY client service. Before
/// this WU only the three clients that passed a <c>rateLimitedAction</c> argument did; every other
/// throttled write (story, chapter, poll vote, lineage, …) surfaced as <see cref="ServerFaultException"/>
/// — "Something went wrong on our end" instead of "wait N seconds". An edge limiter's plain-text 429
/// must stay an <see cref="HttpRequestException"/>, never a <c>JsonException</c>. Client services are
/// constructed over a canned handler, no host (the <c>ClientStoryCoverServiceTests</c> shape). Tier: Unit.
/// </summary>
public class WriteThrottleWireContractTests
{
    private const string ThrottleMessage =
        "You're doing that a little too fast — please wait 42 seconds and try again.";

    // ── Server half ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_AThrottledWrite_Answers429_WithTheKindAndTheRetryInTheExtensions()
    {
        IResult result = await EndpointHelpers.ExecuteAsync(() =>
            throw new WriteRateLimitExceededException(WriteActionKind.Vote, TimeSpan.FromSeconds(29.2)));

        ProblemHttpResult problem = result.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        problem.ProblemDetails.Detail.Should().Contain("30 seconds");
        problem.ProblemDetails.Extensions["retryAfterSeconds"].Should().Be(30d, "the seconds are rounded up");
        problem.ProblemDetails.Extensions["writeActionKind"].Should().Be("Vote");
    }

    [Fact]
    public async Task TheServersBody_RoundTripsIntoTheClientsException_KindAndRetryIntact()
    {
        string body = await RenderAsync(await EndpointHelpers.ExecuteAsync(() =>
            throw new WriteRateLimitExceededException(WriteActionKind.RepeatRequest, TimeSpan.FromSeconds(42))));
        ClientStoryLineageWriteService svc = new(NewClient(new CannedHandler(HttpStatusCode.TooManyRequests, body)));

        Func<Task> act = () => svc.RequestLineageAsync(
            new CreateStoryLineageDto { SourceStoryId = 1, TargetStoryId = 2, TypeId = 1 });

        WriteRateLimitExceededException ex = (await act.Should().ThrowAsync<WriteRateLimitExceededException>()).Which;
        ex.Kind.Should().Be(WriteActionKind.RepeatRequest);
        ex.RetryAfter.Should().Be(TimeSpan.FromSeconds(42));
    }

    // ── Client half ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateListAsync_A429NamingContentCreate_RebuildsTheThrottleException()
    {
        ClientCustomListWriteService svc = new(NewClient(Throttled("ContentCreate", 42)));

        Func<Task> act = () => svc.CreateListAsync("Faves", isPublic: false);

        WriteRateLimitExceededException ex = (await act.Should().ThrowAsync<WriteRateLimitExceededException>()).Which;
        ex.Kind.Should().Be(WriteActionKind.ContentCreate);
        ex.RetryAfter.Should().Be(TimeSpan.FromSeconds(42));
        ExceptionPresenter.IsUserFacing(ex).Should().BeTrue("the user is told to wait, not that something broke");
    }

    /// <summary>
    /// The R7 regression: none of these clients ever passed <c>rateLimitedAction</c>, so before the
    /// shared helper read the kind off the wire each of them answered a throttle with
    /// <see cref="ServerFaultException"/>.
    /// </summary>
    [Theory]
    [MemberData(nameof(ThrottledWrites))]
    public async Task EveryThrottledClientWrite_RebuildsTheExceptionFromTheBody(
        string kind, Func<HttpClient, Task> write)
    {
        Func<Task> act = () => write(NewClient(Throttled(kind, 42)));

        (await act.Should().ThrowAsync<WriteRateLimitExceededException>())
            .Which.Kind.Should().Be(Enum.Parse<WriteActionKind>(kind));
    }

    public static TheoryData<string, Func<HttpClient, Task>> ThrottledWrites() => new()
    {
        { "ContentCreate", http => new ClientStoryArcWriteService(http).CreateArcAsync(new CreateStoryArcDto(1, "Arc", 1, 2)) },
        { "ContentCreate", http => new ClientSeriesWriteService(http).CreateSeriesAsync(new CreateSeriesDto { Name = "S" }) },
        { "ContentCreate", http => new ClientChapterWriteService(http).CreateChapterAsync(new CreateChapterDto { StoryId = 1, ChapterText = "<p>x</p>" }) },
        { "ContentCreate", http => new ClientCustomListWriteService(http).CloneListAsync(3) },
        { "Vote", http => new ClientPollWriteService(http).VoteAsync(5, [1], voteAnonymously: false) },
        { "RepeatRequest", http => new ClientStoryAcknowledgmentWriteService(http).RequestAcknowledgmentAsync(
            new CreateStoryAcknowledgmentDto { StoryId = 1, AcknowledgedUserId = 2, AcknowledgmentRoleId = 1 }) },
        { "Message", http => new ClientMessagingWriteService(http).SendMessageAsync(7, "<p>hi</p>") },
        // The three former rateLimitedAction callers still work with the argument gone.
        { "Comment", http => new ClientCommentWriteService(http).PostUserProfileCommentAsync(new PostUserProfileCommentDto(2, null, "<p>hi</p>")) },
        { "ContentCreate", http => new ClientRecommendationWriteService(http).SubmitAsync(new RecommendationSubmitDto(1, "<p>good</p>")) },
        { "Report", http => new ClientReportSubmissionService(http).SubmitReportAsync(new SubmitReportRequest(ReportedEntityType.Story, 1, 1, null)) },
    };

    [Fact]
    public async Task AnEdgeLimitersPlainText429_IsAnHttpRequestException_NotAJsonException()
    {
        // Program.cs OnRejected: a small plain-text body, no ProblemDetails. The strict reader used to
        // throw JsonException on it from inside the error path.
        var handler = new CannedHandler(HttpStatusCode.TooManyRequests,
            "Too many requests - please slow down and try again shortly.", "text/plain");
        ClientCustomListWriteService svc = new(NewClient(handler));

        Func<Task> act = () => svc.CreateListAsync("Faves", isPublic: false);

        (await act.Should().ThrowExactlyAsync<HttpRequestException>())
            .Which.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Theory]
    [InlineData("""{"status":429,"detail":"slow down"}""")]                                // no kind
    [InlineData("""{"status":429,"detail":"slow down","writeActionKind":"Bogus"}""")]     // unknown name
    [InlineData("""{"status":429,"detail":"slow down","writeActionKind":"99"}""")]        // undefined number
    public async Task A429WhoseBodyNamesNoKnownKind_IsAnHttpRequestException(string body)
    {
        ClientSeriesWriteService svc = new(NewClient(new CannedHandler(HttpStatusCode.TooManyRequests, body)));

        Func<Task> act = () => svc.CreateSeriesAsync(new CreateSeriesDto { Name = "S" });

        (await act.Should().ThrowExactlyAsync<HttpRequestException>())
            .Which.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task A429WithNoRetryExtension_FallsBackToSixtySeconds()
    {
        var handler = new CannedHandler(HttpStatusCode.TooManyRequests,
            """{"status":429,"detail":"slow down","writeActionKind":"Vote"}""");
        ClientPollWriteService svc = new(NewClient(handler));

        Func<Task> act = () => svc.VoteAsync(5, [1], voteAnonymously: false);

        (await act.Should().ThrowAsync<WriteRateLimitExceededException>())
            .Which.RetryAfter.Should().Be(TimeSpan.FromSeconds(60));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static CannedHandler Throttled(string kind, int retryAfterSeconds) => new(
        HttpStatusCode.TooManyRequests,
        $$"""{"type":"https://tools.ietf.org/html/rfc6585#section-4","title":"Too Many Requests","status":429,"detail":"{{ThrottleMessage}}","retryAfterSeconds":{{retryAfterSeconds}},"writeActionKind":"{{kind}}"}""");

    /// <summary>Executes a minimal-API result the way the host would and returns the body it wrote.</summary>
    private static async Task<string> RenderAsync(IResult result)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddProblemDetails();
        DefaultHttpContext httpContext = new() { RequestServices = services.BuildServiceProvider() };
        using MemoryStream body = new();
        httpContext.Response.Body = body;

        await result.ExecuteAsync(httpContext);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        return Encoding.UTF8.GetString(body.ToArray());
    }

    private static HttpClient NewClient(HttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("https://localhost/") };

    private sealed class CannedHandler(HttpStatusCode status, string body, string mediaType = "application/problem+json")
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, mediaType)
            });
    }
}
