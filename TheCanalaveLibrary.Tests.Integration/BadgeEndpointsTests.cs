using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="BadgeEndpoints"/> — the Layer-5 HTTP surface over
/// <see cref="IBadgeReadService"/>/<see cref="IBadgeWriteService"/>, exercised through
/// <c>Factory.CreateClient()</c> so the endpoint's own logic (not just the service) runs for real.
/// Regression coverage for MA-601: the endpoints used to trust a client-supplied <c>userId</c>
/// query parameter, letting any authenticated caller self-award any badge to someone else, read
/// another user's hidden-badge curation view, or overwrite another user's <c>DisplayOrder</c>.
/// Tier: Integration.
/// </summary>
[Collection("Postgres")]
public class BadgeEndpointsTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private async Task<UserBadge?> LoadUserBadgeAsync(int userId, string badgeKey)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.UserBadges.FirstOrDefaultAsync(ub => ub.UserId == userId && ub.BadgeKey == badgeKey);
    }

    private async Task AwardDirectlyAsync(int userId, string badgeKey)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IBadgeWriteService>().AwardAsync(userId, badgeKey);
    }

    [Fact]
    public async Task AwardRoute_IsNotMapped_NoBadgeCanBeMintedOverHttp()
    {
        int callerId = await SeedUserAsync("caller");
        int targetId = await SeedUserAsync("target");
        SetActiveUser(callerId);

        HttpClient client = Factory.CreateClient();
        // Awards are earned, server-internally — no HTTP route may exist (MA-601 hardening; see
        // BadgeEndpoints class doc). Both the old attacker shape and a self-award must dead-end.
        HttpResponseMessage response = await client.PostAsync(
            $"/api/badges/award?userId={targetId}&badgeKey={SiteBadges.Recommender}", content: null);

        // Unmapped API routes fall through to the Blazor SSR catch-all, so the exact status varies
        // (404, or 400 from its antiforgery check on POSTs) — the invariant is: not a success, and
        // no badge row minted for anyone.
        response.IsSuccessStatusCode.Should().BeFalse("the /award route must not be mapped at all");
        (await LoadUserBadgeAsync(callerId, SiteBadges.Recommender)).Should().BeNull();
        (await LoadUserBadgeAsync(targetId, SiteBadges.Recommender)).Should().BeNull();
    }

    [Fact]
    public async Task GetCuration_ClientSuppliedUserId_ReturnsCallersOwnListNotTargets()
    {
        int callerId = await SeedUserAsync("caller");
        int targetId = await SeedUserAsync("target");
        await AwardDirectlyAsync(targetId, SiteBadges.Recommender);
        SetActiveUser(callerId);

        HttpClient client = Factory.CreateClient();
        // Attacker-controlled query string — pre-fix this read targetId's hidden curation view.
        HttpResponseMessage response = await client.GetAsync($"/api/badges?userId={targetId}");

        response.EnsureSuccessStatusCode();
        List<EarnedBadgeDto>? result = await response.Content.ReadFromJsonAsync<List<EarnedBadgeDto>>();
        result.Should().NotBeNull().And.BeEmpty(
            "the caller has no badges of their own — the target's badge must not leak (MA-601)");
    }

    [Fact]
    public async Task SetDisplayOrder_ClientSuppliedUserId_DoesNotTamperWithTarget()
    {
        int callerId = await SeedUserAsync("caller");
        int targetId = await SeedUserAsync("target");
        await AwardDirectlyAsync(targetId, SiteBadges.Recommender); // defaults to DisplayOrder = 1
        SetActiveUser(callerId);

        HttpClient client = Factory.CreateClient();
        // Attacker-controlled query string — pre-fix this hid targetId's badge (DisplayOrder = 0).
        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/badges/display-order?userId={targetId}", new List<string>());

        response.EnsureSuccessStatusCode();
        UserBadge? targetBadge = await LoadUserBadgeAsync(targetId, SiteBadges.Recommender);
        targetBadge.Should().NotBeNull();
        targetBadge!.DisplayOrder.Should().Be(1,
            "a client-supplied userId must never let the caller reorder/hide another user's badges (MA-601)");
    }
}
