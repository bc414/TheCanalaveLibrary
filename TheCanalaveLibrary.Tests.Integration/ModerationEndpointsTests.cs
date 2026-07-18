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
