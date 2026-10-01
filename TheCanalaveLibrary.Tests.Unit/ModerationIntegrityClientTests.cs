using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using TheCanalaveLibrary.Client;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// The Unit-tier half of WU-ModerationIntegrity (2026-09-30):
/// <list type="bullet">
///   <item>The shared <see cref="ActiveUserContextExtensions.RequireModerator"/> guard (owner ruling D9 —
///   one copy, one pair of exception semantics for every role gate).</item>
///   <item><see cref="ClientReportSubmissionService"/>, the WASM twin of the member-facing interface
///   split out of the mod surface: routes, 400 → <see cref="ModerationValidationException"/> (a duplicate
///   report's message reaches the reporter), and 429 → <see cref="WriteRateLimitExceededException"/>
///   (submission sits behind the Report throttle; without <c>rateLimitedAction</c> a 429 would become a
///   generic server fault).</item>
///   <item><see cref="ClientModerationWriteService.ReinstateUserAsync"/>'s route.</item>
/// </list>
/// Clients are constructed directly over a canned <see cref="HttpMessageHandler"/>, no host. Tier: Unit.
/// </summary>
public class ModerationIntegrityClientTests
{
    // ── RequireModerator ─────────────────────────────────────────────────────────

    [Fact]
    public void RequireModerator_Anonymous_IsInvalidOperation_TheUnauthenticatedBranch()
    {
        Action act = () => new StubActiveUserContext().RequireModerator();
        act.Should().Throw<InvalidOperationException>("anonymous maps to 401 — the same type RequireUserId throws");
    }

    [Fact]
    public void RequireModerator_SignedInNonModerator_IsUnauthorizedAccess()
    {
        Action act = () => new StubActiveUserContext { UserId = 5, IsAuthenticated = true }.RequireModerator();
        act.Should().Throw<UnauthorizedAccessException>().WithMessage("*Moderator*",
            "a signed-in non-moderator is forbidden (403), not unauthenticated");
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)] // IsInRole is literal — Admin does not inherit Moderator, so both are listed
    [InlineData(true, true)]
    public void RequireModerator_ModeratorOrAdmin_ReturnsTheirId(bool isModerator, bool isAdmin)
    {
        new StubActiveUserContext { UserId = 7, IsAuthenticated = true, IsModerator = isModerator, IsAdmin = isAdmin }
            .RequireModerator().Should().Be(7);
    }

    // ── ClientReportSubmissionService ────────────────────────────────────────────

    [Fact]
    public async Task GetReportReasonsAsync_GetsTheReasonsRoute()
    {
        var handler = new CannedHandler(HttpStatusCode.OK, """[{"reasonId":2,"reasonName":"Spam","description":null}]""");
        ClientReportSubmissionService svc = new(NewClient(handler));

        ReportReasonDto[] reasons = await svc.GetReportReasonsAsync();

        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/moderation/report-reasons");
        reasons.Should().ContainSingle(r => r.ReasonName == "Spam");
    }

    [Fact]
    public async Task SubmitReportAsync_PostsTheReportsRouteWithABody()
    {
        var handler = new CannedHandler(HttpStatusCode.NoContent, "");
        ClientReportSubmissionService svc = new(NewClient(handler));

        await svc.SubmitReportAsync(new SubmitReportRequest(ReportedEntityType.Story, 42, 2, "notes"));

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/moderation/reports");
        SubmitReportRequest? sent = await handler.LastRequest.Content!.ReadFromJsonAsync<SubmitReportRequest>();
        sent.Should().Be(new SubmitReportRequest(ReportedEntityType.Story, 42, 2, "notes"));
    }

    [Fact]
    public async Task SubmitReportAsync_400_IsAModerationValidationException_WithTheServerText()
    {
        var handler = new CannedHandler(HttpStatusCode.BadRequest,
            """{"detail":"You've already reported this — a moderator will review your open report."}""");
        ClientReportSubmissionService svc = new(NewClient(handler));

        Func<Task> act = () => svc.SubmitReportAsync(new SubmitReportRequest(ReportedEntityType.Story, 42, 2, null));

        (await act.Should().ThrowAsync<ModerationValidationException>())
            .Which.Errors.Should().Equal("You've already reported this — a moderator will review your open report.");
    }

    [Fact]
    public async Task SubmitReportAsync_429_IsAWriteRateLimitExceededException_ForTheReportThrottle()
    {
        var handler = new CannedHandler(HttpStatusCode.TooManyRequests, """{"detail":"Slow down.","retryAfterSeconds":30}""");
        ClientReportSubmissionService svc = new(NewClient(handler));

        Func<Task> act = () => svc.SubmitReportAsync(new SubmitReportRequest(ReportedEntityType.Story, 42, 2, null));

        WriteRateLimitExceededException ex = (await act.Should().ThrowAsync<WriteRateLimitExceededException>()).Which;
        ex.Kind.Should().Be(WriteActionKind.Report);
        ex.RetryAfter.Should().Be(TimeSpan.FromSeconds(30));
    }

    // ── ClientModerationWriteService.ReinstateUserAsync ──────────────────────────

    [Fact]
    public async Task ReinstateUserAsync_PostsTheReinstateRouteWithAnEscapedReason()
    {
        var handler = new CannedHandler(HttpStatusCode.NoContent, "");
        ClientModerationWriteService svc = new(NewClient(handler));

        await svc.ReinstateUserAsync(42, "Appeal upheld & noted");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be(
            "/api/moderation/users/42/reinstate?reason=Appeal%20upheld%20%26%20noted");
    }

    [Fact]
    public async Task ReinstateUserAsync_400_IsAModerationValidationException()
    {
        var handler = new CannedHandler(HttpStatusCode.BadRequest, """{"detail":"This account is already active."}""");
        ClientModerationWriteService svc = new(NewClient(handler));

        Func<Task> act = () => svc.ReinstateUserAsync(42, "x");

        (await act.Should().ThrowAsync<ModerationValidationException>())
            .Which.Errors.Should().Equal("This account is already active.");
    }

    // ── Plumbing ──────────────────────────────────────────────────────────────

    private static HttpClient NewClient(CannedHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://localhost/") };

    /// <summary>Returns one canned response and records the last request for URL/verb assertions.</summary>
    private sealed class CannedHandler(HttpStatusCode status, string jsonBody) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Buffer the body before the caller disposes the request content.
            if (request.Content is not null) await request.Content.LoadIntoBufferAsync(cancellationToken);
            LastRequest = request;
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
