using System.Net;
using FluentAssertions;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// HTTP-surface tests for the mod-only edge gates on <see cref="ExternalVerificationEndpoints"/>
/// (Feature 53, WU39) — mirrors <c>ModerationEndpointsTests</c>: moderator routes carry the named
/// <c>AuthorizationPolicies.RequireModerator</c> policy, so an authenticated non-mod caller is
/// rejected 403 at the edge, and an anonymous caller is rejected 401, before the service (and its
/// own <c>RequireModerator()</c> defense-in-depth gate) ever runs. No rows are seeded — the edge
/// rejection must fire without touching the database. Service-level gate behavior is covered by
/// <see cref="ExternalVerificationTests"/>. Tier: Integration.
/// </summary>
[Collection("Postgres")]
public class ExternalVerificationEndpointsTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task GetPendingAccounts_AuthenticatedNonModerator_Returns403()
    {
        int userId = await SeedUserAsync("non-mod");
        SetActiveUser(userId); // authenticated, IsModerator = false

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/external-verification/pending-accounts");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "the RequireModerator policy blocks non-mods at the edge (MA-702 pattern)");
    }

    [Fact]
    public async Task GetPendingAccounts_Anonymous_Returns401()
    {
        // Base InitializeAsync leaves the active user anonymous.
        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/external-verification/pending-accounts");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ApproveAccount_AuthenticatedNonModerator_Returns403()
    {
        int userId = await SeedUserAsync("non-mod");
        SetActiveUser(userId);

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsync("/api/external-verification/accounts/1/approve", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ApproveLink_AuthenticatedNonModerator_Returns403()
    {
        int userId = await SeedUserAsync("non-mod");
        SetActiveUser(userId);

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsync("/api/external-verification/links/1/approve", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetMyAccounts_Anonymous_Returns401()
    {
        // Author-only reads carry the plain RequireAuthorization() floor.
        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/external-verification/my-accounts");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
