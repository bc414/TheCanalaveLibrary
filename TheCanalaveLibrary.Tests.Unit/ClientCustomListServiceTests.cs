using System.Net;
using System.Text;
using FluentAssertions;
using TheCanalaveLibrary.Client;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="ClientCustomListReadService"/> / <see cref="ClientCustomListWriteService"/>
/// (Feature 51, WU-CustomLists) — constructed directly over a canned <see cref="HttpMessageHandler"/>,
/// no host (mirrors <c>ClientTagServiceTests</c>). Pins the Layer-5 boundary: request URL/verb
/// shapes (which must match <c>CustomListEndpoints</c> character-for-character), the empty-body →
/// null contract on the nullable detail read, and the status-code → contract-exception mapping.
/// Tier: Unit.
/// </summary>
public class ClientCustomListServiceTests
{
    // ── Reads ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMyListsAsync_GetsMineRouteAndDeserializes()
    {
        var handler = new CannedHandler(HttpStatusCode.OK,
            """[{"customListId":3,"listName":"Faves","isPublic":true,"dateCreated":"2026-07-13T00:00:00Z","storyCount":2}]""");
        ClientCustomListReadService svc = new(NewClient(handler));

        List<CustomListSummaryDto> lists = await svc.GetMyListsAsync();

        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/custom-lists/mine");
        lists.Should().ContainSingle(l => l.ListName == "Faves" && l.StoryCount == 2);
    }

    [Fact]
    public async Task GetListDetailAsync_EmptyBody_ReturnsNull()
    {
        // ASP.NET writes an EMPTY 200 body for a null result — the nullable-read contract
        // (layer5-wasm.md §"The Error-Translation Contract").
        var handler = new CannedHandler(HttpStatusCode.OK, "");
        ClientCustomListReadService svc = new(NewClient(handler));

        CustomListDetailDto? detail = await svc.GetListDetailAsync(41);

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/custom-lists/41");
        detail.Should().BeNull();
    }

    [Fact]
    public async Task GetListStoryIdsAsync_EncodesSortQuery()
    {
        var handler = new CannedHandler(HttpStatusCode.OK, "[9,4]");
        ClientCustomListReadService svc = new(NewClient(handler));

        IReadOnlyList<int> ids = await svc.GetListStoryIdsAsync(7, CustomListSortEnum.TitleAsc);

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/custom-lists/7/story-ids?sort=2");
        ids.Should().Equal(9, 4); // order preserved as returned
    }

    [Fact]
    public async Task GetMyListMembershipsAsync_EncodesStoryIdQuery()
    {
        var handler = new CannedHandler(HttpStatusCode.OK, "[]");
        ClientCustomListReadService svc = new(NewClient(handler));

        await svc.GetMyListMembershipsAsync(55);

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/custom-lists/memberships?storyId=55");
    }

    // ── Write happy paths ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateListAsync_EscapesNameAndReturnsId()
    {
        var handler = new CannedHandler(HttpStatusCode.OK, "12");
        ClientCustomListWriteService svc = new(NewClient(handler));

        int id = await svc.CreateListAsync("Best of & More", isPublic: false);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should()
            .Be("/api/custom-lists?listName=Best%20of%20%26%20More&isPublic=False");
        id.Should().Be(12);
    }

    [Fact]
    public async Task AddStoryAsync_PostsCompositeRoute()
    {
        var handler = new CannedHandler(HttpStatusCode.NoContent, "");
        ClientCustomListWriteService svc = new(NewClient(handler));

        await svc.AddStoryAsync(3, 99);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/custom-lists/3/stories/99");
    }

    [Fact]
    public async Task RemoveStoryAsync_DeletesCompositeRoute()
    {
        var handler = new CannedHandler(HttpStatusCode.NoContent, "");
        ClientCustomListWriteService svc = new(NewClient(handler));

        await svc.RemoveStoryAsync(3, 99);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Delete);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/custom-lists/3/stories/99");
    }

    [Fact]
    public async Task CloneListAsync_PostsCloneRouteAndReturnsNewId()
    {
        var handler = new CannedHandler(HttpStatusCode.OK, "77");
        ClientCustomListWriteService svc = new(NewClient(handler));

        int id = await svc.CloneListAsync(8);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/custom-lists/8/clone");
        id.Should().Be(77);
    }

    // ── Status-code → contract-exception translation ──────────────────────────

    [Fact]
    public async Task CreateListAsync_400_ThrowsCustomListValidationExceptionWithProblemDetail()
    {
        var handler = new CannedHandler(HttpStatusCode.BadRequest,
            """{"title":"Bad Request","status":400,"detail":"You already have a list named \"Dup\"."}""");
        ClientCustomListWriteService svc = new(NewClient(handler));

        Func<Task> act = () => svc.CreateListAsync("Dup", false);

        (await act.Should().ThrowAsync<CustomListValidationException>())
            .WithMessage("""You already have a list named "Dup".""");
    }

    [Fact]
    public async Task RenameListAsync_Forbidden_ThrowsUnauthorizedAccessException()
    {
        var handler = new CannedHandler(HttpStatusCode.Forbidden, "");
        ClientCustomListWriteService svc = new(NewClient(handler));

        Func<Task> act = () => svc.RenameListAsync(4, "Blocked");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RenameListAsync_Unauthorized_ThrowsInvalidOperationException()
    {
        // The server maps the services' "…requires an authenticated user" InvalidOperationException
        // to 401 (EndpointHelpers.ExecuteWriteAsync) — the shared client translation (MA-008)
        // reconstructs it, tolerating the cookie handler's body-less 401.
        var handler = new CannedHandler(HttpStatusCode.Unauthorized, "");
        ClientCustomListWriteService svc = new(NewClient(handler));

        Func<Task> act = () => svc.RenameListAsync(4, "Blocked");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AddStoryAsync_404_ThrowsKeyNotFoundException()
    {
        var handler = new CannedHandler(HttpStatusCode.NotFound, "");
        ClientCustomListWriteService svc = new(NewClient(handler));

        Func<Task> act = () => svc.AddStoryAsync(4, 999);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task DeleteListAsync_UnmappedFailureStatus_ThrowsHttpRequestException()
    {
        var handler = new CannedHandler(HttpStatusCode.InternalServerError, "");
        ClientCustomListWriteService svc = new(NewClient(handler));

        Func<Task> act = () => svc.DeleteListAsync(3);

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
