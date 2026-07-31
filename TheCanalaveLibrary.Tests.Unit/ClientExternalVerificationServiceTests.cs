using System.Net;
using System.Text;
using FluentAssertions;
using TheCanalaveLibrary.Client;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="ClientExternalVerificationReadService"/> /
/// <see cref="ClientExternalVerificationWriteService"/> (Feature 53, WU39) — constructed directly
/// over a canned <see cref="HttpMessageHandler"/>, no host. Mirrors <c>ClientGroupServiceTests</c>:
/// pins the Layer-5 boundary — request URL/verb shapes and the status-code → contract-exception
/// mapping. Tier: Unit.
/// </summary>
public class ClientExternalVerificationServiceTests
{
    // ── Reads ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetVerificationPlatformsAsync_GetsPlatformsRoute()
    {
        var handler = new CannedHandler(HttpStatusCode.OK,
            """[{"externalPlatformId":1,"name":"Archive of Our Own","placementInstructions":"Add it to your bio."}]""");
        ClientExternalVerificationReadService svc = new(NewClient(handler));

        IReadOnlyList<VerificationPlatformDto> platforms = await svc.GetVerificationPlatformsAsync();

        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/external-verification/platforms");
        platforms.Should().ContainSingle(p => p.Name == "Archive of Our Own");
    }

    [Fact]
    public async Task GetMyExternalAccountsAsync_GetsMyAccountsRoute()
    {
        var handler = new CannedHandler(HttpStatusCode.OK, "[]");
        ClientExternalVerificationReadService svc = new(NewClient(handler));

        await svc.GetMyExternalAccountsAsync();

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/external-verification/my-accounts");
    }

    [Fact]
    public async Task GetPendingAccountVerificationsAsync_GetsPendingAccountsRoute()
    {
        var handler = new CannedHandler(HttpStatusCode.OK, "[]");
        ClientExternalVerificationReadService svc = new(NewClient(handler));

        await svc.GetPendingAccountVerificationsAsync();

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/external-verification/pending-accounts");
    }

    [Fact]
    public async Task GetPendingLinkVerificationsAsync_GetsPendingLinksRoute()
    {
        var handler = new CannedHandler(HttpStatusCode.OK, "[]");
        ClientExternalVerificationReadService svc = new(NewClient(handler));

        await svc.GetPendingLinkVerificationsAsync();

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/external-verification/pending-links");
    }

    // ── Writes — author ───────────────────────────────────────────────────────

    [Fact]
    public async Task EnsureMyVerificationCodeAsync_PostsMyCodeRoute_ReturnsCode()
    {
        var handler = new CannedHandler(HttpStatusCode.OK, "\"TCL-Verify-ABC234\"");
        ClientExternalVerificationWriteService svc = new(NewClient(handler));

        string code = await svc.EnsureMyVerificationCodeAsync();

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/external-verification/my-code");
        code.Should().Be("TCL-Verify-ABC234");
    }

    [Fact]
    public async Task SubmitAccountForVerificationAsync_PostsAccountsRouteWithBody()
    {
        var handler = new CannedHandler(HttpStatusCode.NoContent, "");
        ClientExternalVerificationWriteService svc = new(NewClient(handler));

        await svc.SubmitAccountForVerificationAsync(
            new AddExternalAccountRequest(1, "https://archiveofourown.org/users/gengarlover", "gengarlover"));

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/external-verification/accounts");
        (await handler.LastRequest.Content!.ReadAsStringAsync()).Should().Contain("gengarlover");
    }

    [Fact]
    public async Task RequestLinkVerificationAsync_PostsRequestRoute()
    {
        var handler = new CannedHandler(HttpStatusCode.NoContent, "");
        ClientExternalVerificationWriteService svc = new(NewClient(handler));

        await svc.RequestLinkVerificationAsync(42);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/external-verification/links/42/request");
    }

    // ── Writes — moderator ────────────────────────────────────────────────────

    [Fact]
    public async Task ApproveAccountVerificationAsync_PostsApproveRoute()
    {
        var handler = new CannedHandler(HttpStatusCode.NoContent, "");
        ClientExternalVerificationWriteService svc = new(NewClient(handler));

        await svc.ApproveAccountVerificationAsync(7);

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/external-verification/accounts/7/approve");
    }

    [Fact]
    public async Task RejectAccountVerificationAsync_PostsRejectRouteWithReasonQueryParam()
    {
        var handler = new CannedHandler(HttpStatusCode.NoContent, "");
        ClientExternalVerificationWriteService svc = new(NewClient(handler));

        await svc.RejectAccountVerificationAsync(7, "Code not found on profile.");

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be(
            "/api/external-verification/accounts/7/reject?reason=Code%20not%20found%20on%20profile.");
    }

    [Fact]
    public async Task ApproveLinkVerificationAsync_PostsApproveRoute()
    {
        var handler = new CannedHandler(HttpStatusCode.NoContent, "");
        ClientExternalVerificationWriteService svc = new(NewClient(handler));

        await svc.ApproveLinkVerificationAsync(42);

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/external-verification/links/42/approve");
    }

    [Fact]
    public async Task RejectLinkVerificationAsync_PostsRejectRouteWithReasonQueryParam()
    {
        var handler = new CannedHandler(HttpStatusCode.NoContent, "");
        ClientExternalVerificationWriteService svc = new(NewClient(handler));

        await svc.RejectLinkVerificationAsync(42, "Author mismatch.");

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be(
            "/api/external-verification/links/42/reject?reason=Author%20mismatch.");
    }

    // ── Status-code → contract-exception translation ────────────────────────

    [Fact]
    public async Task ApproveAccountVerificationAsync_Unauthorized_ThrowsSessionExpiredException()
    {
        var handler = new CannedHandler(HttpStatusCode.Unauthorized, "");
        ClientExternalVerificationWriteService svc = new(NewClient(handler));

        Func<Task> act = () => svc.ApproveAccountVerificationAsync(7);

        // WU-ErrorHandling2 (2026-07-30): 401 is a session signal, not authorization denial.
        await act.Should().ThrowAsync<SessionExpiredException>();
    }

    [Fact]
    public async Task ApproveAccountVerificationAsync_Forbidden_ThrowsUnauthorizedAccessException()
    {
        var handler = new CannedHandler(HttpStatusCode.Forbidden, "");
        ClientExternalVerificationWriteService svc = new(NewClient(handler));

        Func<Task> act = () => svc.ApproveAccountVerificationAsync(7);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RequestLinkVerificationAsync_NotFound_ThrowsKeyNotFoundException()
    {
        var handler = new CannedHandler(HttpStatusCode.NotFound, "");
        ClientExternalVerificationWriteService svc = new(NewClient(handler));

        Func<Task> act = () => svc.RequestLinkVerificationAsync(999);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ApproveLinkVerificationAsync_UnmappedFailureStatus_ThrowsHttpRequestException()
    {
        var handler = new CannedHandler(HttpStatusCode.InternalServerError, "");
        ClientExternalVerificationWriteService svc = new(NewClient(handler));

        Func<Task> act = () => svc.ApproveLinkVerificationAsync(42);

        await act.Should().ThrowAsync<HttpRequestException>();
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
