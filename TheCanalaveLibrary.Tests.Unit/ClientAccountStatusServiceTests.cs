using System.Net;
using System.Text;
using FluentAssertions;
using TheCanalaveLibrary.Client;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="ClientAccountStatusReadService"/> (WU-AccountEnforcement) —
/// constructed directly over a canned <see cref="HttpMessageHandler"/>, no host. Pins the
/// Layer-5 boundary: request URL/verb shape and DTO deserialization, including the
/// <c>SuspendedUntilUtc</c> field the banner needs for its Suspended copy. Tier: Unit.
/// </summary>
public class ClientAccountStatusServiceTests
{
    [Fact]
    public async Task GetMyAccountStatusAsync_GetsAccountStatusRouteAndDeserializes()
    {
        var handler = new CannedHandler(HttpStatusCode.OK, """{"status":0,"suspendedUntilUtc":null}""");
        ClientAccountStatusReadService svc = new(NewClient(handler));

        AccountStatusDto dto = await svc.GetMyAccountStatusAsync();

        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/account-status");
        dto.Status.Should().Be(AccountStatusEnum.Active);
        dto.SuspendedUntilUtc.Should().BeNull();
    }

    [Fact]
    public async Task GetMyAccountStatusAsync_Suspended_DeserializesSuspendedUntilUtc()
    {
        var handler = new CannedHandler(HttpStatusCode.OK,
            """{"status":2,"suspendedUntilUtc":"2026-08-15T00:00:00Z"}""");
        ClientAccountStatusReadService svc = new(NewClient(handler));

        AccountStatusDto dto = await svc.GetMyAccountStatusAsync();

        dto.Status.Should().Be(AccountStatusEnum.Suspended);
        dto.SuspendedUntilUtc.Should().Be(new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc));
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
