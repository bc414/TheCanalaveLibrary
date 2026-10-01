using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// HTTP-surface tests for the mod-only edge gates on <see cref="ModerationEndpoints"/> and
/// <see cref="SiteSettingsEndpoints"/> (MA-702, endpoint-authz sweep 2026-07-18): moderator writes
/// now carry the named <c>AuthorizationPolicies.RequireModerator</c> policy at the endpoint, so an
/// authenticated non-mod caller is rejected 403 at the edge — before the service (and its own
/// <c>RequireModerator()</c> defense-in-depth gate) ever runs. No report/setting rows are seeded:
/// the edge rejection must fire without touching the database. Service-level gate behavior is
/// covered by <see cref="ModerationServiceTests"/>. Tier: Integration.
/// </summary>
[Collection("Postgres")]
public class ModerationEndpointsTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task ClaimReport_AuthenticatedNonModerator_Returns403()
    {
        int userId = await SeedUserAsync("non-mod");
        SetActiveUser(userId); // authenticated, IsModerator = false

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsync("/api/moderation/reports/123/claim", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "the RequireModerator policy blocks non-mods at the edge — the handler (and its DB lookup) " +
            "must never run for a non-mod caller (MA-702, endpoint-authz sweep 2026-07-18)");
    }

    // ── WU-StoryLifecycle (D1) ──────────────────────────────────────────────────

    [Fact]
    public async Task SetAutoApprove_AuthenticatedNonModerator_Returns403()
    {
        int userId = await SeedUserAsync("non-mod");
        SetActiveUser(userId);

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsync(
            "/api/moderation/users/123/auto-approve?enabled=false&reasonId=1&reason=x", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ApproveSubmission_NotPending_Returns400_NotTheOld401()
    {
        // Before WU-StoryLifecycle the "not pending approval" guard threw InvalidOperationException,
        // which EndpointHelpers maps to 401 (the auth safety net) — a moderator's WASM session saw
        // "session expired" for what was a business rule. It is now ModerationValidationException.
        int modId = await SeedUserAsync("mod");
        int storyId = await SeedStoryAsync(await SeedUserAsync("author"), status: StoryStatusEnum.InProgress);
        SetActiveUser(FakeActiveUserContext.Moderator(modId));

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsync($"/api/moderation/submissions/{storyId}/approve", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("already handled");
    }

    [Fact]
    public async Task ApproveSubmission_UnknownStory_Returns404()
    {
        int modId = await SeedUserAsync("mod");
        SetActiveUser(FakeActiveUserContext.Moderator(modId));

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsync("/api/moderation/submissions/999999/approve", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SetSiteSetting_AuthenticatedNonModerator_Returns403()
    {
        int userId = await SeedUserAsync("non-mod");
        SetActiveUser(userId); // authenticated, IsModerator = false

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/site-settings/{SiteSettingKeys.SpotlightPositionCount}", 99);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "site-setting writes carry the same edge RequireModerator policy — a non-mod must not " +
            "reach the write service (MA-702, endpoint-authz sweep 2026-07-18)");
    }
}
