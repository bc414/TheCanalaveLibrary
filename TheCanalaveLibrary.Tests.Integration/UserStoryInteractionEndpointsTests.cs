using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="UserStoryInteractionEndpoints"/> — the Layer-5 HTTP surface
/// over <see cref="IUserStoryInteractionReadService"/>, exercised through
/// <c>Factory.CreateClient()</c>. Regression coverage for the endpoint-authz sweep (2026-07-18):
/// <c>GET /api/user-story-interactions/favorites/{userId}</c> used to bind <c>includePrivate</c>
/// from the query string, letting any caller read another user's hidden favorites by passing
/// <c>?includePrivate=true</c>. The flag is now derived server-side
/// (<c>activeUser.UserId == userId</c>) and the query-string value is ignored — same derivation
/// pattern as the MA-602 profile fix. Tier: Integration.
/// </summary>
[Collection("Postgres")]
public class UserStoryInteractionEndpointsTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _ownerId;
    private int _publicFavoriteStoryId;
    private int _hiddenFavoriteStoryId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _ownerId               = await SeedUserAsync("owner");
        _publicFavoriteStoryId = await SeedStoryAsync();
        _hiddenFavoriteStoryId = await SeedStoryAsync();

        // FK parents (user + story rows) seeded above; the interaction rows go in directly.
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.UserStoryInteractions.Add(new UserStoryInteraction
        {
            UserId = _ownerId, StoryId = _publicFavoriteStoryId, IsFavorite = true
        });
        db.UserStoryInteractions.Add(new UserStoryInteraction
        {
            UserId = _ownerId, StoryId = _hiddenFavoriteStoryId, IsFavorite = true, IsHiddenFavorite = true
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetFavorites_OtherUserWithIncludePrivateTrue_ExcludesHiddenFavorites()
    {
        int otherId = await SeedUserAsync("other");
        SetActiveUser(otherId);

        HttpClient client = Factory.CreateClient();
        // Attacker-controlled query string — pre-fix, this exposed the owner's hidden favorites.
        HttpResponseMessage response =
            await client.GetAsync($"/api/user-story-interactions/favorites/{_ownerId}?includePrivate=true");

        response.EnsureSuccessStatusCode();
        int[]? ids = await response.Content.ReadFromJsonAsync<int[]>();
        ids.Should().NotBeNull();
        ids.Should().Contain(_publicFavoriteStoryId);
        ids.Should().NotContain(_hiddenFavoriteStoryId,
            "includePrivate is derived server-side from viewer == owner — a client-asserted query flag " +
            "must never unlock another user's hidden favorites (endpoint-authz sweep 2026-07-18)");
    }

    [Fact]
    public async Task GetFavorites_Owner_IncludesHiddenFavorites()
    {
        SetActiveUser(_ownerId);

        HttpClient client = Factory.CreateClient();
        // No query flag needed — the server derives includePrivate from the authenticated viewer.
        HttpResponseMessage response =
            await client.GetAsync($"/api/user-story-interactions/favorites/{_ownerId}");

        response.EnsureSuccessStatusCode();
        int[]? ids = await response.Content.ReadFromJsonAsync<int[]>();
        ids.Should().NotBeNull();
        ids.Should().Contain([_publicFavoriteStoryId, _hiddenFavoriteStoryId],
            "the owner always sees their own hidden favorites");
    }

    // ── Feature 30 entry points (owner ruling D3, WU-InertFeatures) ──────────────

    [Fact]
    public async Task ReadItLaterFromRecommendation_Anonymous_Returns401()
    {
        int recId = await SeedApprovedRecAsync(_publicFavoriteStoryId);
        SetActiveUser(FakeActiveUserContext.Anonymous());

        HttpResponseMessage response = await Factory.CreateClient().PostAsync(
            $"/api/user-story-interactions/read-it-later/from-recommendation/{recId}", null);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReadItLaterFromRecommendation_Authenticated_Returns204_AndRecordsTheAttribution()
    {
        int recId = await SeedApprovedRecAsync(_publicFavoriteStoryId);
        int readerId = await SeedUserAsync("ril-reader");
        SetActiveUser(readerId);

        HttpResponseMessage response = await Factory.CreateClient().PostAsync(
            $"/api/user-story-interactions/read-it-later/from-recommendation/{recId}", null);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.UserStoryRecommendationSources.Should().ContainSingle(s =>
            s.UserId == readerId && s.StoryId == _publicFavoriteStoryId && s.SourceRecommendationId == recId);
    }

    [Fact]
    public async Task Started_WithRecommendationIdQuery_BindsItAsTheDirectLinkAttribution()
    {
        int recId = await SeedApprovedRecAsync(_publicFavoriteStoryId);
        int readerId = await SeedUserAsync("direct-reader");
        SetActiveUser(readerId);

        HttpResponseMessage response = await Factory.CreateClient().PostAsync(
            $"/api/user-story-interactions/{_publicFavoriteStoryId}/started?recommendationId={recId}", null);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.UserStoryRecommendationSources.Should().ContainSingle(s =>
            s.UserId == readerId && s.SourceRecommendationId == recId);
    }

    private async Task<int> SeedApprovedRecAsync(int storyId)
    {
        int recommenderId = await SeedUserAsync();
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Recommendation rec = new()
        {
            StoryId = storyId,
            RecommenderId = recommenderId,
            StatusId = (short)RecommendationStatusEnum.Approved,
            DatePosted = DateTime.UtcNow,
            RecommendationDetail = new RecommendationDetail { Text = "<p>endorsement</p>" },
        };
        db.Recommendations.Add(rec);
        await db.SaveChangesAsync();
        return rec.RecommendationId;
    }
}
