using System.Net;
using System.Text;
using FluentAssertions;
using TheCanalaveLibrary.Client;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="ClientGroupReadService"/> / <see cref="ClientGroupWriteService"/>
/// (F38/F39/F40, WU-GroupsL5, 2026-07-24) — constructed directly over a canned
/// <see cref="HttpMessageHandler"/>, no host. Mirrors <c>ClientTagServiceTests</c>: pins the
/// Layer-5 boundary translation — request URL/verb shapes, the <see cref="PagedResult{T}"/>
/// deconstruction back to tuples, and the status-code → contract-exception mapping, including the
/// project's one non-standard case (Groups' 403 disambiguation between a plain member/admin gate
/// and the content-rating waterfall). Folder-write methods get explicit coverage since
/// <see cref="GroupFolderManagementPage"/> is their only UI consumer.
/// Tier: Unit.
/// </summary>
public class ClientGroupServiceTests
{
    // ── Reads ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetListingsAsync_GetsListingsRouteAndDeconstructsPagedResult()
    {
        var handler = new CannedHandler(HttpStatusCode.OK,
            """{"items":[{"groupId":1,"groupName":"Alpha","description":null,"audienceType":0,"memberCount":3,"dateCreated":"2026-01-01T00:00:00Z"}],"totalCount":5}""");
        ClientGroupReadService svc = new(NewClient(handler));

        (GroupCardDto[] items, int totalCount) = await svc.GetListingsAsync(page: 2, pageSize: 10);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/groups?page=2&pageSize=10");
        items.Should().ContainSingle(g => g.GroupName == "Alpha");
        totalCount.Should().Be(5, "TotalCount must survive the PagedResult deconstruction");
    }

    [Fact]
    public async Task GetByIdAsync_EmptyBody_ReturnsNull()
    {
        // GetNullableFromJsonAsync's empty-body-means-null contract (ClientHttpHelpers) —
        // ASP.NET writes an empty 200 body for Results.Json(null).
        var handler = new CannedHandler(HttpStatusCode.OK, "");
        ClientGroupReadService svc = new(NewClient(handler));

        GroupDetailDto? detail = await svc.GetByIdAsync(42);

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/groups/42");
        detail.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_PopulatedBody_DeserializesGroupStoryDtoInStoriesAndFolderTree()
    {
        // WU-GroupsL5b: GroupDetailDto.StoryIds/GroupFolderDto.StoryIds retyped from bare int to
        // GroupStoryDto (GroupStoryId + StoryId) — no earlier test in this file exercised a
        // populated GetByIdAsync body at all (the only prior case sent an empty one), so this pins
        // the client's deserialization of the new nested shape specifically.
        var handler = new CannedHandler(HttpStatusCode.OK,
            """
            {
                "groupId": 7, "groupName": "Group", "description": null, "audienceType": 0,
                "maxContentRating": 2, "creatorId": 1, "creatorDisplayName": "Creator",
                "memberCount": 1, "dateCreated": "2026-01-01T00:00:00Z", "currentUserRole": 1,
                "folderTree": [
                    {
                        "groupFolderId": 100, "groupId": 7, "parentFolderId": null, "name": "Folder",
                        "maxRating": 2, "sortOrder": 0,
                        "stories": [{"groupStoryId": 50, "storyId": 2}],
                        "children": []
                    }
                ],
                "stories": [{"groupStoryId": 51, "storyId": 1}, {"groupStoryId": 50, "storyId": 2}]
            }
            """);
        ClientGroupReadService svc = new(NewClient(handler));

        GroupDetailDto? detail = await svc.GetByIdAsync(7);

        detail.Should().NotBeNull();
        detail!.Stories.Should().BeEquivalentTo(
        [
            new GroupStoryDto(51, 1),
            new GroupStoryDto(50, 2)
        ]);
        detail.FolderTree.Should().ContainSingle().Which.Stories.Should().ContainSingle()
            .Which.Should().Be(new GroupStoryDto(50, 2));
    }

    [Fact]
    public async Task GetGroupGateAsync_GetsGateRoute()
    {
        var handler = new CannedHandler(HttpStatusCode.OK, "");
        ClientGroupReadService svc = new(NewClient(handler));

        await svc.GetGroupGateAsync(7);

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/groups/7/gate");
    }

    [Fact]
    public async Task GetCurrentUserRoleAsync_NonMember_ReturnsNull()
    {
        var handler = new CannedHandler(HttpStatusCode.OK, "");
        ClientGroupReadService svc = new(NewClient(handler));

        GroupRole? role = await svc.GetCurrentUserRoleAsync(7);

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/groups/7/role");
        role.Should().BeNull();
    }

    [Fact]
    public async Task GetMembersAsync_GetsMembersRouteAndDeconstructsPagedResult()
    {
        var handler = new CannedHandler(HttpStatusCode.OK,
            """{"items":[{"userId":9,"displayName":"Admin","avatarUrl":null,"role":1,"dateJoined":"2026-01-01T00:00:00Z"}],"totalCount":1}""");
        ClientGroupReadService svc = new(NewClient(handler));

        (GroupMemberDto[] members, int totalCount) = await svc.GetMembersAsync(7, page: 1, pageSize: 20);

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/groups/7/members?page=1&pageSize=20");
        members.Should().ContainSingle(m => m.UserId == 9 && m.Role == GroupRole.Admin);
        totalCount.Should().Be(1);
    }

    // ── Write happy paths (folder writes get explicit coverage — this page's only UI consumer) ──

    [Fact]
    public async Task CreateFolderAsync_Ok_PostsToFoldersRouteAndReturnsId()
    {
        var handler = new CannedHandler(HttpStatusCode.OK, "99");
        ClientGroupWriteService svc = new(NewClient(handler));

        int folderId = await svc.CreateFolderAsync(new CreateFolderDto { GroupId = 7, Name = "New" });

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/groups/folders");
        folderId.Should().Be(99);
    }

    [Fact]
    public async Task RenameFolderAsync_Ok_PutsRawStringToNameRoute()
    {
        var handler = new CannedHandler(HttpStatusCode.NoContent, "");
        ClientGroupWriteService svc = new(NewClient(handler));

        await svc.RenameFolderAsync(5, "Renamed");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/groups/folders/5/name");
        // [FromBody] string on the server — the client must send the JSON-encoded string as the body.
        (await handler.LastRequest.Content!.ReadAsStringAsync()).Should().Be("\"Renamed\"");
    }

    [Fact]
    public async Task DeleteFolderAsync_NoContent_Completes()
    {
        var handler = new CannedHandler(HttpStatusCode.NoContent, "");
        ClientGroupWriteService svc = new(NewClient(handler));

        await svc.DeleteFolderAsync(5);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Delete);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/groups/folders/5");
    }

    [Fact]
    public async Task ReorderFolderAsync_NoContent_PutsSortOrderAsQueryParam()
    {
        var handler = new CannedHandler(HttpStatusCode.NoContent, "");
        ClientGroupWriteService svc = new(NewClient(handler));

        await svc.ReorderFolderAsync(5, 3);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/groups/folders/5/sort-order?newSortOrder=3");
    }

    [Fact]
    public async Task CreateGroupAsync_Ok_PostsToGroupsRouteAndReturnsId()
    {
        var handler = new CannedHandler(HttpStatusCode.OK, "12");
        ClientGroupWriteService svc = new(NewClient(handler));

        int groupId = await svc.CreateGroupAsync(new CreateGroupDto { GroupName = "New Group" });

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/groups");
        groupId.Should().Be(12);
    }

    // ── Status-code → contract-exception translation (Groups' own private mapping) ──────────

    [Fact]
    public async Task CreateFolderAsync_400_ThrowsGroupValidationExceptionWithProblemDetail()
    {
        var handler = new CannedHandler(HttpStatusCode.BadRequest,
            """{"title":"Bad Request","status":400,"detail":"Folder MaxRating (M) cannot exceed the group's MaxContentRating (T)."}""");
        ClientGroupWriteService svc = new(NewClient(handler));

        Func<Task> act = () => svc.CreateFolderAsync(new CreateFolderDto { GroupId = 7, Name = "Bad" });

        (await act.Should().ThrowAsync<GroupValidationException>())
            .Where(ex => ex.Errors.Single().Contains("cannot exceed"));
    }

    [Fact]
    public async Task CreateFolderAsync_Unauthorized_ThrowsUnauthorizedAccessException()
    {
        var handler = new CannedHandler(HttpStatusCode.Unauthorized, "");
        ClientGroupWriteService svc = new(NewClient(handler));

        Func<Task> act = () => svc.CreateFolderAsync(new CreateFolderDto { GroupId = 7, Name = "X" });

        await act.Should().ThrowAsync<UnauthorizedAccessException>(
            "401 means not signed in at all — distinct from Groups' own 403 gate below");
    }

    [Fact]
    public async Task CreateFolderAsync_ForbiddenNoDetail_ThrowsPlainUnauthorizedAccessException()
    {
        // Empty body 403 = the plain member/admin gate (ServerGroupWriteService.RequireAdminAsync).
        var handler = new CannedHandler(HttpStatusCode.Forbidden, "");
        ClientGroupWriteService svc = new(NewClient(handler));

        Func<Task> act = () => svc.CreateFolderAsync(new CreateFolderDto { GroupId = 7, Name = "X" });

        (await act.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("*Admin*", "the plain-gate message names the required role");
    }

    [Fact]
    public async Task AddStoryAsync_ForbiddenWithDetail_ThrowsContentRatingExceededException()
    {
        // Populated-Detail 403 = the content-rating waterfall, per the class's own disambiguation
        // rule — distinct from the plain gate above despite sharing the same HTTP status.
        var handler = new CannedHandler(HttpStatusCode.Forbidden,
            """{"title":"Forbidden","status":403,"detail":"Story rating M exceeds this group's maximum content rating T."}""");
        ClientGroupWriteService svc = new(NewClient(handler));

        Func<Task> act = () => svc.AddStoryAsync(new AddGroupStoryDto { GroupId = 7, StoryId = 3 });

        (await act.Should().ThrowAsync<ContentRatingExceededException>())
            .WithMessage("*exceeds*");
    }

    [Fact]
    public async Task RenameFolderAsync_404_ThrowsKeyNotFoundException()
    {
        var handler = new CannedHandler(HttpStatusCode.NotFound, "");
        ClientGroupWriteService svc = new(NewClient(handler));

        Func<Task> act = () => svc.RenameFolderAsync(999, "Ghost");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task DeleteFolderAsync_UnmappedFailureStatus_ThrowsHttpRequestException()
    {
        var handler = new CannedHandler(HttpStatusCode.InternalServerError, "");
        ClientGroupWriteService svc = new(NewClient(handler));

        Func<Task> act = () => svc.DeleteFolderAsync(5);

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
