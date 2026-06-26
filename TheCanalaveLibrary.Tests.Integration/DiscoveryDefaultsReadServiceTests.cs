using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="ServerDiscoveryDefaultsReadService"/> (WU28, spec §8.7).
/// Exercises the two-query merge algorithm against real Postgres (Testcontainers).
///
/// <b>Seeded defaults (from EF <c>HasData</c>, survives Respawn):</b>
/// <c>SearchPage × Ignored = true</c> is the only seeded system default for SearchPage.
/// All other keys on SearchPage have no row (no default = not enabled = not in output).
///
/// <b>Per-test seeding plan:</b>
/// <list type="bullet">
///   <item><see cref="GetDefaultExcludedInteractionsAsync_SearchPage_ReturnsIgnoredOnly"/> — anonymous;
///   no user seed needed.</item>
///   <item><see cref="GetDefaultExcludedInteractionsAsync_AnonymousViewer_ReturnsSystemDefaults"/> —
///   anonymous; no user seed needed.</item>
///   <item><see cref="GetDefaultExcludedInteractionsAsync_UserOverride_Enable_AddsKey"/> —
///   seeds one user + one <see cref="UserStoryInteractionFilterSetting"/> row (Favorited=true).</item>
///   <item><see cref="GetDefaultExcludedInteractionsAsync_UserOverride_Disable_RemovesKey"/> —
///   seeds one user + one <see cref="UserStoryInteractionFilterSetting"/> row (Ignored=false).</item>
///   <item><see cref="GetDefaultExcludedInteractionsAsync_HasStartedKey_DroppedFromOutput"/> —
///   anonymous; no user seed needed; verifies that <c>HasStarted</c> key is not in output even if
///   a future migration seeds it as enabled.</item>
/// </list>
/// Tier: Integration (Testcontainers Postgres, real EF, real Respawn).
/// </summary>
[Collection("Postgres")]
public class DiscoveryDefaultsReadServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private FakeActiveUserContext _fake = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _fake = Factory.Services.GetRequiredService<FakeActiveUserContext>();
    }

    // ── System defaults — anonymous viewer ───────────────────────────────────────────

    [Fact]
    public async Task GetDefaultExcludedInteractionsAsync_SearchPage_ReturnsIgnoredOnly()
    {
        // Anonymous viewer → system defaults only.
        _fake.UserId = null;
        _fake.IsAuthenticated = false;

        IReadOnlyList<UserStoryInteractionTypeEnum> result =
            await InvokeAsync(svc => svc.GetDefaultExcludedInteractionsAsync(SiteSearchModes.SearchPage));

        result.Should().BeEquivalentTo(
            [UserStoryInteractionTypeEnum.Ignore],
            "the seeded SearchPage default enables Ignored only; all other keys have no row");
    }

    [Fact]
    public async Task GetDefaultExcludedInteractionsAsync_AnonymousViewer_ReturnsSystemDefaults()
    {
        // Authenticated and anonymous should return the same result for SearchPage
        // when no per-user overrides exist.
        _fake.UserId = null;
        _fake.IsAuthenticated = false;

        IReadOnlyList<UserStoryInteractionTypeEnum> anonResult =
            await InvokeAsync(svc => svc.GetDefaultExcludedInteractionsAsync(SiteSearchModes.SearchPage));

        // Seed a user but add no override rows.
        int userId = await SeedUserAsync();
        _fake.UserId = userId;
        _fake.IsAuthenticated = true;

        IReadOnlyList<UserStoryInteractionTypeEnum> authedNoOverrideResult =
            await InvokeAsync(svc => svc.GetDefaultExcludedInteractionsAsync(SiteSearchModes.SearchPage));

        anonResult.Should().BeEquivalentTo(authedNoOverrideResult,
            "authenticated viewer with no overrides gets the same result as anonymous");
    }

    // ── Per-user overrides ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDefaultExcludedInteractionsAsync_UserOverride_Enable_AddsKey()
    {
        // Seed a user, then add a per-user override that enables Favorited on SearchPage.
        int userId = await SeedUserAsync();
        await SeedUserOverrideAsync(userId, SiteSearchModes.SearchPage,
            UserStoryInteractionFilters.Favorited, isEnabled: true);

        _fake.UserId = userId;
        _fake.IsAuthenticated = true;

        IReadOnlyList<UserStoryInteractionTypeEnum> result =
            await InvokeAsync(svc => svc.GetDefaultExcludedInteractionsAsync(SiteSearchModes.SearchPage));

        result.Should().Contain(UserStoryInteractionTypeEnum.Ignore,
            "system default (Ignored=true) still applies");
        result.Should().Contain(UserStoryInteractionTypeEnum.Favorite,
            "user override enables Favorited so it appears in the merged output");
    }

    [Fact]
    public async Task GetDefaultExcludedInteractionsAsync_UserOverride_Disable_RemovesKey()
    {
        // Seed a user override that disables the Ignored default on SearchPage.
        int userId = await SeedUserAsync();
        await SeedUserOverrideAsync(userId, SiteSearchModes.SearchPage,
            UserStoryInteractionFilters.Ignored, isEnabled: false);

        _fake.UserId = userId;
        _fake.IsAuthenticated = true;

        IReadOnlyList<UserStoryInteractionTypeEnum> result =
            await InvokeAsync(svc => svc.GetDefaultExcludedInteractionsAsync(SiteSearchModes.SearchPage));

        result.Should().NotContain(UserStoryInteractionTypeEnum.Ignore,
            "user override disables Ignored, so it must be absent even though the system default is true");
        result.Should().BeEmpty("with Ignored disabled and no other user overrides, result is empty");
    }

    // ── HasStarted key dropped ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDefaultExcludedInteractionsAsync_HasStartedKey_DroppedFromOutput()
    {
        // Seed a per-user override that enables HasStarted (simulates a future migration seeding it).
        int userId = await SeedUserAsync();
        await SeedUserOverrideAsync(userId, SiteSearchModes.SearchPage,
            UserStoryInteractionFilters.HasStarted, isEnabled: true);

        _fake.UserId = userId;
        _fake.IsAuthenticated = true;

        IReadOnlyList<UserStoryInteractionTypeEnum> result =
            await InvokeAsync(svc => svc.GetDefaultExcludedInteractionsAsync(SiteSearchModes.SearchPage));

        // HasStarted has no UserStoryInteractionTypeEnum counterpart; service drops it silently.
        // The result should contain only Ignore (from the system default) — nothing extra.
        result.Should().Contain(UserStoryInteractionTypeEnum.Ignore,
            "system default Ignored=true is unaffected by the HasStarted override");
        result.Count.Should().Be(1,
            "only Ignore from the system default; HasStarted is silently dropped since it has no enum value");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────

    private async Task<T> InvokeAsync<T>(Func<IDiscoveryDefaultsReadService, Task<T>> call)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IDiscoveryDefaultsReadService svc =
            scope.ServiceProvider.GetRequiredService<IDiscoveryDefaultsReadService>();
        return await call(svc);
    }

    /// <summary>
    /// Inserts one <see cref="UserStoryInteractionFilterSetting"/> row for the given user.
    /// </summary>
    private async Task SeedUserOverrideAsync(int userId, string searchModeKey,
        string filterKey, bool isEnabled)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.UserStoryInteractionFilterSettings.Add(new UserStoryInteractionFilterSetting
        {
            UserId = userId,
            SearchModeKey = searchModeKey,
            UserStoryInteractionFilterKey = filterKey,
            IsEnabled = isEnabled
        });
        await db.SaveChangesAsync();
    }
}
