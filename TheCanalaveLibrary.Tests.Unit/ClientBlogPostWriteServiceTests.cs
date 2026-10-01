using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using TheCanalaveLibrary.Client;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// The WASM twins of the group-post lifecycle (owner ruling D10, WU-TptHardDelete 2026-09-30):
/// <see cref="ClientBlogPostWriteService.UpdateGroupBlogPostAsync"/> and
/// <see cref="ClientBlogPostWriteService.DeleteGroupBlogPostAsync"/>. Pins the verb and the
/// <c>api/blog-posts/group/{id}</c> path the server maps (a profile-route twin would hit the profile
/// gate and 404 every group post), the body, and the shared MA-008 status → exception translation —
/// 403 → <see cref="UnauthorizedAccessException"/>, 404 → <see cref="KeyNotFoundException"/>, 400 →
/// <see cref="BlogPostValidationException"/> — so a component behaves the same on either side.
/// Clients are constructed directly over a canned <see cref="HttpMessageHandler"/>, no host. Tier: Unit.
/// </summary>
public class ClientBlogPostWriteServiceTests
{
    private static UpdateGroupBlogPostDto GroupUpdate(int id) => new()
    {
        BlogPostId = id, Title = "Edited", Content = "<p>Edited</p>", Rating = Rating.T, HasSpoilers = true
    };

    [Fact]
    public async Task UpdateGroupBlogPostAsync_PutsTheGroupRouteWithTheBody()
    {
        var handler = new CannedHandler(HttpStatusCode.NoContent, "");
        ClientBlogPostWriteService svc = new(NewClient(handler));

        await svc.UpdateGroupBlogPostAsync(GroupUpdate(42));

        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/blog-posts/group/42");
        UpdateGroupBlogPostDto? sent = await handler.LastRequest.Content!.ReadFromJsonAsync<UpdateGroupBlogPostDto>();
        sent.Should().BeEquivalentTo(GroupUpdate(42));
    }

    [Fact]
    public async Task DeleteGroupBlogPostAsync_DeletesTheGroupRoute()
    {
        var handler = new CannedHandler(HttpStatusCode.NoContent, "");
        ClientBlogPostWriteService svc = new(NewClient(handler));

        await svc.DeleteGroupBlogPostAsync(42);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Delete);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/blog-posts/group/42");
    }

    [Fact]
    public async Task GroupUpdateAndDelete_403_IsUnauthorizedAccess()
    {
        ClientBlogPostWriteService svc = new(NewClient(new CannedHandler(HttpStatusCode.Forbidden, "")));

        await ((Func<Task>)(() => svc.UpdateGroupBlogPostAsync(GroupUpdate(42)))).Should().ThrowAsync<UnauthorizedAccessException>();
        await ((Func<Task>)(() => svc.DeleteGroupBlogPostAsync(42))).Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GroupUpdateAndDelete_404_IsKeyNotFound()
    {
        ClientBlogPostWriteService svc = new(NewClient(new CannedHandler(HttpStatusCode.NotFound, "")));

        await ((Func<Task>)(() => svc.UpdateGroupBlogPostAsync(GroupUpdate(42)))).Should().ThrowAsync<KeyNotFoundException>();
        await ((Func<Task>)(() => svc.DeleteGroupBlogPostAsync(42))).Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateGroupBlogPostAsync_400_IsABlogPostValidationException_WithTheServerText()
    {
        var handler = new CannedHandler(HttpStatusCode.BadRequest, """{"detail":"Title is required."}""");
        ClientBlogPostWriteService svc = new(NewClient(handler));

        Func<Task> act = () => svc.UpdateGroupBlogPostAsync(GroupUpdate(42));

        (await act.Should().ThrowAsync<BlogPostValidationException>())
            .Which.Errors.Should().Equal("Title is required.");
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
