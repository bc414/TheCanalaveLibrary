using System.Net;
using System.Text;
using FluentAssertions;
using TheCanalaveLibrary.Client;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// The WASM side of the new-message email preference (owner ruling D18, WU-PrivateMessageEmail
/// 2026-10-13): <see cref="ClientMessagingWriteService"/> constructed directly over a canned
/// <see cref="HttpMessageHandler"/>, no host (the <c>ClientStoryDeleteServiceTests</c> shape). Pins the
/// verbs and routes against <c>MessagingEndpoints</c>' <c>GET</c>/<c>PUT /api/messaging/email-preference</c>
/// and the 401 → <see cref="SessionExpiredException"/> translation the settings page's sign-in prompt
/// relies on. The server routes are Integration-covered (<c>PrivateMessageEmailTests</c>). Tier: Unit.
/// </summary>
public class ClientMessagingEmailPreferenceTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public async Task Get_ReadsTheBoolFromThePreferenceRoute(string json, bool expected)
    {
        var handler = new CannedHandler(HttpStatusCode.OK, json);
        ClientMessagingWriteService svc = new(NewClient(handler));

        (await svc.GetMyNewMessageEmailEnabledAsync()).Should().Be(expected);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/messaging/email-preference");
    }

    [Theory]
    [InlineData(true, "/api/messaging/email-preference?enabled=true")]
    [InlineData(false, "/api/messaging/email-preference?enabled=false")]
    public async Task Set_PutsTheValueOnThePreferenceRoute(bool enabled, string expectedPath)
    {
        var handler = new CannedHandler(HttpStatusCode.NoContent);
        ClientMessagingWriteService svc = new(NewClient(handler));

        await svc.SetMyNewMessageEmailEnabledAsync(enabled);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be(expectedPath);
    }

    [Fact]
    public async Task Get_AnExpiredSession_IsSessionExpired()
    {
        ClientMessagingWriteService svc = new(NewClient(new CannedHandler(HttpStatusCode.Unauthorized)));

        Func<Task> act = () => svc.GetMyNewMessageEmailEnabledAsync();

        await act.Should().ThrowAsync<SessionExpiredException>();
    }

    [Fact]
    public async Task Set_AnExpiredSession_IsSessionExpired()
    {
        ClientMessagingWriteService svc = new(NewClient(new CannedHandler(HttpStatusCode.Unauthorized)));

        Func<Task> act = () => svc.SetMyNewMessageEmailEnabledAsync(false);

        await act.Should().ThrowAsync<SessionExpiredException>();
    }

    // ── Plumbing ──────────────────────────────────────────────────────────────

    private static HttpClient NewClient(CannedHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://localhost/") };

    private sealed class CannedHandler(HttpStatusCode status, string? json = null) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            var response = new HttpResponseMessage(status);
            if (json is not null)
                response.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return Task.FromResult(response);
        }
    }
}
