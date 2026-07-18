using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="StoryEndpoints"/> — the Layer-5 HTTP surface over
/// <see cref="IStoryReadService"/>, exercised through <c>Factory.CreateClient()</c>. Regression
/// coverage for the endpoint-authz sweep (2026-07-18): <c>GET /api/stories/{id}/edit</c> now
/// enforces story authorship in <c>ServerStoryReadService.GetStoryForEditAsync</c>
/// (<see cref="UnauthorizedAccessException"/> → 403 via <c>EndpointHelpers.ExecuteWriteAsync</c>,
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
            "UnauthorizedAccessException and ExecuteWriteAsync maps it (endpoint-authz sweep 2026-07-18)");
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
}
