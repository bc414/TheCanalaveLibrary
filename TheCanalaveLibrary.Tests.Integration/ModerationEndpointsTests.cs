using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

    // ── WU-ModerationIntegrity (D9 sub-edge: statuses fixed at the throw sites) ────────

    [Fact]
    public async Task ResolveNoAction_UnknownReport_Returns404_NotTheOld401()
    {
        // The resolve paths loaded the report with SingleAsync, whose InvalidOperationException the
        // shared EndpointHelpers maps to 401 — a moderator's WASM session saw "session expired".
        int modId = await SeedUserAsync("mod");
        SetActiveUser(FakeActiveUserContext.Moderator(modId));

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsync("/api/moderation/reports/999999/resolve-no-action", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SubmitReport_DisallowedTargetType_Returns400_WithTheMessage()
    {
        int userId = await SeedUserAsync("reporter");
        SetActiveUser(userId);

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/moderation/reports",
            new SubmitReportRequest((ReportedEntityType)99, 1, 1, null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "a business rule, not an authentication failure — it was InvalidOperationException → 401");
        (await response.Content.ReadAsStringAsync()).Should().Contain("cannot be reported");
    }

    [Fact]
    public async Task Reinstate_AuthenticatedNonModerator_Returns403()
    {
        int userId = await SeedUserAsync("non-mod");
        SetActiveUser(userId);

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsync("/api/moderation/users/123/reinstate?reason=x", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "the new route carries the edge RequireModerator policy");
    }

    [Fact]
    public async Task Reinstate_Moderator_BannedUser_Returns204_AndTheUserIsActive()
    {
        // L5: routing, the query-bound reason, and the handler reaching ReinstateUserAsync.
        int modId = await SeedUserAsync("mod");
        int target = await SeedUserAsync("banned");
        await SetAccountStatusAsync(target, AccountStatusEnum.Banned);
        SetActiveUser(FakeActiveUserContext.Moderator(modId));

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsync(
            $"/api/moderation/users/{target}/reinstate?reason={Uri.EscapeDataString("Appeal upheld.")}", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Users.Where(u => u.Id == target).Select(u => u.AccountStatus).SingleAsync())
            .Should().Be(AccountStatusEnum.Active);
        (await db.Reports.Where(r => r.ReportedUserId == target).Select(r => r.ActionTaken).SingleAsync())
            .Should().Be("Appeal upheld.", "the reason binds from the query string");
    }

    [Fact]
    public async Task Reinstate_ActiveUser_Returns400_WithTheMessage()
    {
        int modId = await SeedUserAsync("mod");
        int target = await SeedUserAsync("active");
        SetActiveUser(FakeActiveUserContext.Moderator(modId));

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsync($"/api/moderation/users/{target}/reinstate?reason=x", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "a business rule is a 400 — never the 401 an InvalidOperationException would map to");
        (await response.Content.ReadAsStringAsync()).Should().Contain("already active");
    }

    [Fact]
    public async Task UserHistory_UnknownUser_StillReturns404_InsideTheWrapper()
    {
        int modId = await SeedUserAsync("mod");
        SetActiveUser(FakeActiveUserContext.Moderator(modId));

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/moderation/users/999999/history");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "the history read now runs inside ExecuteAsync (its service can throw) and keeps its 404");
    }

    private async Task SetAccountStatusAsync(int userId, AccountStatusEnum status)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountStatus, status));
    }
}
