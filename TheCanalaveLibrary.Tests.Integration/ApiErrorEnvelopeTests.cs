using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for the read-endpoint half of WU-ErrorHandling2 (2026-07-30) — endpoints
/// that were unwrapped before this WU (a typed service exception fell through to an unhandled
/// 500) now wrap in <see cref="EndpointHelpers.ExecuteAsync"/> and answer with a bodied,
/// status-correct <c>ProblemDetails</c>. Pins the boundary the same way <see cref="TagEndpointsTests"/>
/// does for the original write-side contract. Tier: Integration.
/// </summary>
[Collection("Postgres")]
public class ApiErrorEnvelopeTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    // ── TreeSearchEndpoints — Validate(request)'s ArgumentException ─────────────

    [Fact]
    public async Task TreeSearchTraverse_NeitherRootSet_Returns400WithDetailMessage()
    {
        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/tree-search/traverse", new TreeSearchRequest
        {
            RootStoryId = null,
            RootUserId = null,
            EdgeTypes = [TreeSearchEdgeType.Favorite],
            MaxDegrees = 2,
            ResultCap = 50,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Exactly one of");
    }

    [Fact]
    public async Task TreeSearchTraverse_NoEdgeTypes_Returns400()
    {
        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/tree-search/traverse", new TreeSearchRequest
        {
            RootStoryId = 1,
            EdgeTypes = [],
            MaxDegrees = 2,
            ResultCap = 50,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── StoryEndpoints — ApplyFiltersAsync's ValidateShipShape StoryValidationException ──
    // Found live by this WU's audit: WU-TagFanon upgraded the exception type to a user-facing
    // one but never wrapped these read handlers, so malformed ship input still 500'd.

    [Fact]
    public async Task StoryQuery_TooManyShipMembers_Returns400NotUnhandled500()
    {
        HttpClient client = Factory.CreateClient();
        StoryFilterDto filter = new()
        {
            IncludedShips = [new ShipFilterDto { MemberTagIds = [1, 2, 3, 4] }], // MaxMembers = 3
        };

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/stories/query", filter);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        // EndpointHelpers.ExecuteAsync's 400 arm reads ex.Message, not ex.Errors —
        // StoryValidationException.Message is always the fixed base text (the per-item ship-filter
        // detail lives in .Errors, which ExceptionPresenter reads client-side, not Detail over the
        // wire; pre-existing shape, unrelated to this WU). The regression this pins is the status
        // code: 400, not an unhandled 500.
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Story validation failed");
    }

    [Fact]
    public async Task StoryRandomBatch_DuplicateShipMember_Returns400()
    {
        HttpClient client = Factory.CreateClient();
        StoryFilterDto filter = new()
        {
            ExcludedShips = [new ShipFilterDto { MemberTagIds = [1, 1] }],
        };

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/stories/random-batch?batchSize=10", filter);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Story validation failed");
    }

    [Fact]
    public async Task StoryFilterCandidates_TooManyShipMembers_Returns400()
    {
        HttpClient client = Factory.CreateClient();
        int storyId = await SeedStoryAsync();
        StoryFilterDto filter = new()
        {
            IncludedShips = [new ShipFilterDto { MemberTagIds = [1, 2, 3, 4] }],
        };

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/stories/filter-candidates?candidateIds={storyId}", filter);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── MessagingEndpoints — the membership-guard KeyNotFoundException + auth safety net ──

    [Fact]
    public async Task MessagingConversationThread_UnknownConversation_Returns404()
    {
        int userId = await SeedUserAsync("MsgReader");
        SetActiveUser(userId);

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response =
            await client.GetAsync("/api/messaging/conversations/999999?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MessagingConversations_Authenticated_Returns200()
    {
        // Regression net for the wrap itself: an authenticated, non-exceptional call must still
        // succeed once GetConversationsAsync is wrapped in ExecuteAsync.
        int userId = await SeedUserAsync("MsgLister");
        SetActiveUser(userId);

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/messaging/conversations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
