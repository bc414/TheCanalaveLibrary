using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TheCanalaveLibrary.Client;
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

    // ── StoryEndpoints — ResolveExpansionAsync's ValidateShipShape StoryValidationException ──
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

    // ── The validation 400's error list (WU-ParityAndRemaining, 2026-10-13) ─────────
    // Service audit §2.7.1: the server joined a validation exception's error list into Detail only, so
    // WASM rebuilt a one-line list. For stories Detail is the fixed "Story validation failed.", so the
    // user saw that sentence instead of the real errors.

    [Fact]
    public async Task StoryCreate_EmptyFields_Returns400_WhoseErrorsExtensionListsEveryMessage()
    {
        int userId = await SeedUserAsync("EnvelopeAuthor");
        SetActiveUser(userId);

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/stories", new CreateStoryDTO
        {
            Title = "", ShortDescription = "", LongDescription = "",
            Rating = Rating.E, PostApprovalStatus = StoryStatusEnum.InProgress,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("detail").GetString().Should().Be("Story validation failed.");
        body.RootElement.GetProperty("errors").EnumerateArray().Select(e => e.GetString())
            .Should().Contain(["Title is required", "Short description is required", "Long description is required"]);
    }

    [Fact]
    public async Task StoryCreate_TheWasmClientRebuildsTheSameErrorList_TheCircuitShows()
    {
        // The whole hop: the real endpoint answers, the real client service translates. The list the
        // client's exception carries must be the one the service threw.
        int userId = await SeedUserAsync("EnvelopeClient");
        SetActiveUser(userId);
        CreateStoryDTO invalid = new()
        {
            Title = "", ShortDescription = "", LongDescription = "",
            Rating = Rating.E, PostApprovalStatus = StoryStatusEnum.InProgress,
        };
        List<string> expected = invalid.CanSave();

        ClientStoryWriteService client = new(Factory.CreateClient());
        Func<Task> act = () => client.CreateStoryAsync(invalid);

        (await act.Should().ThrowAsync<StoryValidationException>())
            .Which.Errors.Should().Equal(expected);
    }

    [Fact]
    public async Task ANonValidation400_CarriesNoErrorsExtension()
    {
        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/tree-search/traverse", new TreeSearchRequest
        {
            RootStoryId = null, RootUserId = null, EdgeTypes = [TreeSearchEdgeType.Favorite],
            MaxDegrees = 2, ResultCap = 50,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.TryGetProperty("errors", out _).Should().BeFalse("an ArgumentException 400 stays detail-only");
    }

    // ── GET /api/client-config (WU-ParityAndRemaining; service audit §2.7.3) ─────────

    [Fact]
    public async Task ClientConfig_IsAnonymous_AndCarriesTheServersSpriteRoot()
    {
        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/client-config");

        response.StatusCode.Should().Be(HttpStatusCode.OK, "the WASM host fetches it before any sign-in state matters");
        ClientConfigDto? config = await response.Content.ReadFromJsonAsync<ClientConfigDto>();
        config.Should().Be(new ClientConfigDto("/sprites/themes"), "the shared host leaves Sprites:BaseUrl unset");
    }

    [Fact]
    public async Task ClientConfig_TheWasmLoaderAdoptsWhatTheServerServes()
    {
        ClientRuntimeConfig runtime = new();

        await runtime.LoadAsync(Factory.CreateClient(), NullLogger.Instance);

        runtime.SpriteBaseUrl.Should().Be(
            Factory.Services.GetRequiredService<ClientConfigDto>().SpriteBaseUrl,
            "the client half reads the server's one registered value, never a second copy");
    }
}
