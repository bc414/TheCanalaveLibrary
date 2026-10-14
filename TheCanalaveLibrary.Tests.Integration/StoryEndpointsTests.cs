using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Client;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="StoryEndpoints"/> — the Layer-5 HTTP surface over
/// <see cref="IStoryReadService"/>, exercised through <c>Factory.CreateClient()</c>. Regression
/// coverage for the endpoint-authz sweep (2026-07-18): <c>GET /api/stories/{id}/edit</c> now
/// enforces story authorship in <c>ServerStoryReadService.GetStoryForEditAsync</c>
/// (<see cref="UnauthorizedAccessException"/> → 403 via <c>EndpointHelpers.ExecuteAsync</c>,
/// same wire shape as the MA-301 chapter-edit route), and
/// <c>GET /api/stories/by-author/{authorId}</c> no longer keys its ContentRating-filter bypass to
/// the client-supplied <c>authorId</c> — only the author's own list is an elevated read. Also pins
/// MA-302: the anonymous reading-progress scroll ping no-ops with 202 instead of 401.
/// Tier: Integration.
/// </summary>
[Collection("Postgres")]
public class StoryEndpointsTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    // ASP.NET's HttpResultsHelper writes an EMPTY 200 body for a null Results.Json(...) result —
    // ReadFromJsonAsync throws on that. Mirrors UserProfileEndpointsTests.ReadNullableAsync.
    private static async Task<T?> ReadNullableAsync<T>(HttpResponseMessage response)
    {
        string raw = await response.Content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(raw) || raw == "null"
            ? default
            : JsonSerializer.Deserialize<T>(raw, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    // ── GET /api/stories/{id}/edit — author-only editor read ─────────────────────

    [Fact]
    public async Task GetEdit_NonAuthor_Returns403()
    {
        int authorId = await SeedUserAsync("author");
        int storyId  = await SeedStoryAsync(authorId);
        int otherId  = await SeedUserAsync("other");
        SetActiveUser(otherId);

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync($"/api/stories/{storyId}/edit");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a non-author must get a translated 403 — GetStoryForEditAsync's author gate throws " +
            "UnauthorizedAccessException and ExecuteAsync maps it (endpoint-authz sweep 2026-07-18)");
    }

    [Fact]
    public async Task GetEdit_Author_Returns200WithDto()
    {
        int authorId = await SeedUserAsync("author");
        int storyId  = await SeedStoryAsync(authorId);
        SetActiveUser(authorId);

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync($"/api/stories/{storyId}/edit");

        response.StatusCode.Should().Be(HttpStatusCode.OK, "the author may load their own story for editing");
        StoryUpdateDTO? dto = await ReadNullableAsync<StoryUpdateDTO>(response);
        dto.Should().NotBeNull();
        dto!.StoryId.Should().Be(storyId);
    }

    [Fact]
    public async Task GetEdit_Anonymous_Returns401()
    {
        int authorId = await SeedUserAsync("author");
        int storyId  = await SeedStoryAsync(authorId);
        SetActiveUser(FakeActiveUserContext.Anonymous());

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync($"/api/stories/{storyId}/edit");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the /edit route carries RequireAuthorization() — anonymous callers stop at the auth floor");
    }

    // ── POST /api/stories/{id}/status — author lifecycle move (WU-StoryLifecycle, D1) ──

    [Fact]
    public async Task PostStatus_Author_Returns200WithTheResultingStatus()
    {
        int authorId = await SeedUserAsync("author");
        int storyId  = await SeedStoryAsync(authorId, status: StoryStatusEnum.Draft);
        SetActiveUser(authorId);

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsync(
            $"/api/stories/{storyId}/status?target={(short)StoryStatusEnum.PendingApproval}", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<StoryStatusEnum>()).Should().Be(StoryStatusEnum.PendingApproval,
            "an untrusted author's submit enters the queue — the body is the RESULTING status");
    }

    [Theory]
    [InlineData((short)StoryStatusEnum.Rejected)] // illegal: only a moderator rejects
    [InlineData((short)99)]                       // undefined enum value — binds, then the service refuses
    public async Task PostStatus_IllegalOrUndefinedTarget_Returns400(short target)
    {
        int authorId = await SeedUserAsync("author");
        int storyId  = await SeedStoryAsync(authorId, status: StoryStatusEnum.Draft);
        SetActiveUser(authorId);

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsync($"/api/stories/{storyId}/status?target={target}", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostStatus_Anonymous_Returns401()
    {
        int authorId = await SeedUserAsync("author");
        int storyId  = await SeedStoryAsync(authorId, status: StoryStatusEnum.Draft);
        SetActiveUser(FakeActiveUserContext.Anonymous());

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsync($"/api/stories/{storyId}/status?target=1", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostStatus_NonAuthor_Returns403()
    {
        int authorId = await SeedUserAsync("author");
        int storyId  = await SeedStoryAsync(authorId, status: StoryStatusEnum.Draft);
        SetActiveUser(await SeedUserAsync("other"));

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsync($"/api/stories/{storyId}/status?target=1", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── GET /api/stories/by-author/{authorId} — elevated read is owner-only ──────

    [Fact]
    public async Task GetByAuthor_OtherUserWithoutMature_ExcludesMatureStoryIds()
    {
        int authorId      = await SeedUserAsync("author");
        int matureStoryId = await SeedStoryAsync(authorId, Rating.M);
        int safeStoryId   = await SeedStoryAsync(authorId, Rating.E);
        int otherId       = await SeedUserAsync("other");
        SetActiveUser(otherId); // ShowMatureContent = false

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync($"/api/stories/by-author/{authorId}");

        response.EnsureSuccessStatusCode();
        int[]? ids = await response.Content.ReadFromJsonAsync<int[]>();
        ids.Should().NotBeNull();
        ids.Should().Contain(safeStoryId);
        ids.Should().NotContain(matureStoryId,
            "the ContentRating-filter bypass must never be keyed to the client-supplied authorId — " +
            "another viewer gets the normally-filtered set (endpoint-authz sweep 2026-07-18)");
    }

    [Fact]
    public async Task GetByAuthor_AuthorThemself_IncludesOwnMatureStoryIds()
    {
        int authorId      = await SeedUserAsync("author");
        int matureStoryId = await SeedStoryAsync(authorId, Rating.M);
        int safeStoryId   = await SeedStoryAsync(authorId, Rating.E);
        SetActiveUser(authorId); // ShowMatureContent = false — the elevated own-list read must still bypass

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync($"/api/stories/by-author/{authorId}");

        response.EnsureSuccessStatusCode();
        int[]? ids = await response.Content.ReadFromJsonAsync<int[]>();
        ids.Should().NotBeNull();
        ids.Should().Contain([safeStoryId, matureStoryId],
            "authors always see their own full story list regardless of their rating setting");
    }

    // ── POST /api/reading-progress — MA-302 anonymous scroll ping ────────────────

    [Fact]
    public async Task ReadingProgress_Anonymous_Returns202NoOp()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsync("/api/reading-progress/?chapterId=1&progress=0.5", null);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "MA-302: the buffered reading-progress ping silently no-ops for anonymous viewers — a 401 " +
            "here threw HttpRequestException out of the [JSInvokable] scroll handler on the public reading page");
    }

    // ── /api/stories/{id}/cover — upload persists, DELETE clears (owner ruling D14, WU-BlobCleanup) ──

    private async Task<HttpResponseMessage> PostCoverAsync(int storyId)
    {
        using MultipartFormDataContent form = new();
        ByteArrayContent file = new(TestImages.Png(4, 4));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "cover");
        return await Factory.CreateClient().PostAsync($"/api/stories/{storyId}/cover", form);
    }

    private async Task<string?> StoredCoverAsync(int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().StoryListings
            .Where(l => l.StoryId == storyId).Select(l => l.CoverArtRelativeUrl).SingleAsync();
    }

    [Fact]
    public async Task PostCover_Author_Returns200_AndThePathIsAlreadyPersisted_AndEchoedByEdit()
    {
        int authorId = await SeedUserAsync("author");
        int storyId  = await SeedStoryAsync(authorId);
        SetActiveUser(authorId);

        HttpResponseMessage response = await PostCoverAsync(storyId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string? path = await response.Content.ReadFromJsonAsync<string>();
        path.Should().MatchRegex($@"^/uploads/stories/{storyId}/cover-[0-9a-f-]+\.png$");
        (await StoredCoverAsync(storyId)).Should().Be(path, "no follow-up PUT is needed (D14)");

        StoryEditDTO? edit = await ReadNullableAsync<StoryEditDTO>(
            await Factory.CreateClient().GetAsync($"/api/stories/{storyId}/edit"));
        edit!.CoverArtRelativeUrl.Should().Be(path, "GET /edit echoes the cover over the wire (WASM parity)");
    }

    [Fact]
    public async Task DeleteCover_Author_Returns204_AndClearsTheColumn()
    {
        int authorId = await SeedUserAsync("author");
        int storyId  = await SeedStoryAsync(authorId);
        SetActiveUser(authorId);
        (await PostCoverAsync(storyId)).EnsureSuccessStatusCode();

        HttpResponseMessage response = await Factory.CreateClient().DeleteAsync($"/api/stories/{storyId}/cover");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StoredCoverAsync(storyId)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteCover_NonAuthor_Returns403_AndTheCoverStays()
    {
        int authorId = await SeedUserAsync("author");
        int storyId  = await SeedStoryAsync(authorId);
        SetActiveUser(authorId);
        string? path = await (await PostCoverAsync(storyId)).Content.ReadFromJsonAsync<string>();
        SetActiveUser(await SeedUserAsync("other"));

        HttpResponseMessage response = await Factory.CreateClient().DeleteAsync($"/api/stories/{storyId}/cover");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await StoredCoverAsync(storyId)).Should().Be(path);
    }

    [Fact]
    public async Task DeleteCover_Anonymous_Returns401()
    {
        int authorId = await SeedUserAsync("author");
        int storyId  = await SeedStoryAsync(authorId);
        SetActiveUser(FakeActiveUserContext.Anonymous());

        HttpResponseMessage response = await Factory.CreateClient().DeleteAsync($"/api/stories/{storyId}/cover");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteCover_MissingStory_Returns404()
    {
        SetActiveUser(await SeedUserAsync("author"));

        HttpResponseMessage response = await Factory.CreateClient().DeleteAsync($"/api/stories/{int.MaxValue}/cover");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE /api/stories/{id} — the author story delete (owner ruling D15, WU-AuthorStoryDelete) ──

    private async Task<bool> StoryExistsAsync(int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Stories.AnyAsync(s => s.StoryId == storyId);
    }

    [Fact]
    public async Task Delete_Author_Returns204_AndTheStoryIsGone()
    {
        int authorId = await SeedUserAsync("author");
        int storyId  = await SeedStoryAsync(authorId);
        SetActiveUser(authorId);

        HttpResponseMessage response = await Factory.CreateClient().DeleteAsync($"/api/stories/{storyId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StoryExistsAsync(storyId)).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_NonAuthor_Returns403_AndTheStoryStays()
    {
        int authorId = await SeedUserAsync("author");
        int storyId  = await SeedStoryAsync(authorId);
        SetActiveUser(await SeedUserAsync("other"));

        HttpResponseMessage response = await Factory.CreateClient().DeleteAsync($"/api/stories/{storyId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await StoryExistsAsync(storyId)).Should().BeTrue();
    }

    [Fact]
    public async Task Delete_Anonymous_Returns401_AndTheStoryStays()
    {
        int authorId = await SeedUserAsync("author");
        int storyId  = await SeedStoryAsync(authorId);
        SetActiveUser(FakeActiveUserContext.Anonymous());

        HttpResponseMessage response = await Factory.CreateClient().DeleteAsync($"/api/stories/{storyId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await StoryExistsAsync(storyId)).Should().BeTrue();
    }

    [Fact]
    public async Task Delete_MissingStory_Returns404()
    {
        SetActiveUser(await SeedUserAsync("author"));

        HttpResponseMessage response = await Factory.CreateClient().DeleteAsync($"/api/stories/{int.MaxValue}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    // ── POST /api/stories/query — the WASM listing read, through the real client ──────────────
    // An absent restrictToStoryIds binds as an empty array, which the service reads as "narrow to
    // nothing": every unrestricted WASM listing came back empty, so /discover's Date-published sort
    // showed no stories on WASM (WU-TrackerFixes browser pass, 2026-10-13). The endpoint now reads an
    // absent key as unrestricted, and the client answers an empty restrict set itself.

    [Fact]
    public async Task Query_WithNoRestrictSet_IsUnrestricted()
    {
        int storyId = await SeedStoryAsync(await SeedUserAsync("author"));
        SetActiveUser(FakeActiveUserContext.Anonymous());

        (StoryListingDto[] items, int total) = await new ClientStoryReadService(Factory.CreateClient())
            .GetListingsAsync(new StoryFilterDto { Sort = DefaultSortOrder.DatePublished });

        items.Should().ContainSingle(i => i.StoryId == storyId, "no restrict set means the whole visible library");
        total.Should().Be(1);
    }

    [Fact]
    public async Task Query_WithAnEmptyRestrictSet_NarrowsToNothing()
    {
        await SeedStoryAsync(await SeedUserAsync("author"));
        SetActiveUser(FakeActiveUserContext.Anonymous());

        (StoryListingDto[] items, int total) = await new ClientStoryReadService(Factory.CreateClient())
            .GetListingsAsync(new StoryFilterDto { Sort = DefaultSortOrder.DatePublished }, restrictToStoryIds: []);

        items.Should().BeEmpty("an empty candidate set is an empty shelf, never the whole library");
        total.Should().Be(0);
    }

    [Fact]
    public async Task Query_WithARestrictSet_ListsOnlyItsMembers()
    {
        int authorId = await SeedUserAsync("author");
        int kept = await SeedStoryAsync(authorId);
        await SeedStoryAsync(authorId);
        SetActiveUser(FakeActiveUserContext.Anonymous());

        (StoryListingDto[] items, int total) = await new ClientStoryReadService(Factory.CreateClient())
            .GetListingsAsync(new StoryFilterDto { Sort = DefaultSortOrder.DatePublished }, restrictToStoryIds: [kept]);

        items.Select(i => i.StoryId).Should().Equal(kept);
        total.Should().Be(1);
    }
}
