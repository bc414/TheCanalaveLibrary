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
}
