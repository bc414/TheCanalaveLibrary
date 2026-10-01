using System.Net;
using System.Text;
using FluentAssertions;
using TheCanalaveLibrary.Client;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// The WASM side of WU-StoryLifecycle's two new write surfaces plus the pulled-forward moderation
/// 400 mapping (service audit §2.7.5) — constructed directly over a canned
/// <see cref="HttpMessageHandler"/>, no host (mirrors <c>ClientCustomListServiceTests</c>). Pins the
/// route/verb shapes against <c>StoryEndpoints</c>/<c>ModerationEndpoints</c>, the resulting-status
/// round-trip, and that a moderator guard's 400 reaches the page as the user-facing
/// <see cref="ModerationValidationException"/> rather than a generic error.
/// Tier: Unit.
/// </summary>
public class ClientStoryLifecycleServiceTests
{
    [Fact]
    public async Task TransitionStatusAsync_PostsNumericTarget_AndReturnsTheResultingStatus()
    {
        // The server answers with the RESULTING status (a trusted submit lands published).
        var handler = new CannedHandler(HttpStatusCode.OK, ((short)StoryStatusEnum.Completed).ToString());
        ClientStoryWriteService svc = new(NewClient(handler));

        StoryStatusEnum result = await svc.TransitionStatusAsync(7, StoryStatusEnum.PendingApproval);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/stories/7/status?target=1");
        result.Should().Be(StoryStatusEnum.Completed);
    }

    [Fact]
    public async Task TransitionStatusAsync_400_ThrowsStoryValidationException()
    {
        var handler = new CannedHandler(HttpStatusCode.BadRequest, """{"detail":"Story validation failed."}""");
        ClientStoryWriteService svc = new(NewClient(handler));

        Func<Task> act = () => svc.TransitionStatusAsync(7, StoryStatusEnum.Rejected);

        await act.Should().ThrowAsync<StoryValidationException>();
    }

    [Fact]
    public async Task SetCanAutoApproveAsync_PostsUserRouteWithEscapedQuery()
    {
        var handler = new CannedHandler(HttpStatusCode.NoContent, "");
        ClientModerationWriteService svc = new(NewClient(handler));

        await svc.SetCanAutoApproveAsync(42, canAutoApprove: false, reasonId: 3, reason: "Spam & more");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be(
            "/api/moderation/users/42/auto-approve?enabled=False&reasonId=3&reason=Spam%20%26%20more");
    }

    [Fact]
    public async Task ApproveStoryAsync_400_ThrowsModerationValidationException_WithTheServerText()
    {
        // Before WU-StoryLifecycle this arm built an ArgumentException, which ExceptionPresenter
        // treats as non-user-facing — the guard text collapsed to the generic error on WASM.
        var handler = new CannedHandler(HttpStatusCode.BadRequest, """{"detail":"This submission was already handled."}""");
        ClientModerationWriteService svc = new(NewClient(handler));

        Func<Task> act = () => svc.ApproveStoryAsync(5);

        (await act.Should().ThrowAsync<ModerationValidationException>())
            .Which.Errors.Should().Equal("This submission was already handled.");
    }

    // ── Plumbing ──────────────────────────────────────────────────────────────

    private static HttpClient NewClient(CannedHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://localhost/") };

    /// <summary>Returns one canned response and records the last request for URL/verb assertions.</summary>
    private sealed class CannedHandler(HttpStatusCode status, string jsonBody) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            });
        }
    }
}
