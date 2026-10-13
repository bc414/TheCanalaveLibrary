using System.Net;
using System.Text;
using FluentAssertions;
using TheCanalaveLibrary.Client;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// The WASM side of the author story delete (owner ruling D15, WU-AuthorStoryDelete 2026-10-13) —
/// <see cref="ClientStoryWriteService.DeleteStoryAsync"/> constructed directly over a canned
/// <see cref="HttpMessageHandler"/>, no host (the <c>ClientStoryCoverServiceTests</c> shape). Pins the verb
/// and route against <c>StoryEndpoints</c>' <c>DELETE /api/stories/{id}</c> and the status → exception
/// translation the editor page relies on. The server route is Integration-covered (<c>StoryEndpointsTests</c>).
/// Tier: Unit.
/// </summary>
public class ClientStoryDeleteServiceTests
{
    [Fact]
    public async Task DeleteStoryAsync_SendsABodilessDelete_ToTheStoryRoute()
    {
        var handler = new CannedHandler(HttpStatusCode.NoContent);
        ClientStoryWriteService svc = new(NewClient(handler));

        await svc.DeleteStoryAsync(7);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Delete);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/stories/7",
            "the story route itself — not /cover, which only clears the cover");
        handler.HadBody.Should().BeFalse();
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, typeof(UnauthorizedAccessException))]
    [InlineData(HttpStatusCode.Unauthorized, typeof(SessionExpiredException))]
    [InlineData(HttpStatusCode.NotFound, typeof(KeyNotFoundException))]
    public async Task DeleteStoryAsync_TranslatesTheRefusal(HttpStatusCode status, Type expected)
    {
        ClientStoryWriteService svc = new(NewClient(new CannedHandler(status)));

        Func<Task> act = () => svc.DeleteStoryAsync(7);

        (await act.Should().ThrowAsync<Exception>()).Which.Should().BeOfType(expected);
    }

    // ── Plumbing ──────────────────────────────────────────────────────────────

    private static HttpClient NewClient(CannedHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://localhost/") };

    private sealed class CannedHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public bool HadBody { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            HadBody = request.Content is not null;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent("", Encoding.UTF8, "application/json")
            });
        }
    }
}
