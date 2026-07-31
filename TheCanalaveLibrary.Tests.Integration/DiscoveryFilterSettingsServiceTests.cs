using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="ServerDiscoveryFilterSettingsService"/>
/// (WU-DiscoveryOverrideUI, closes tracker item B7 — spec §8.7). Exercises the write half
/// <see cref="ServerDiscoveryDefaultsReadService"/> never carried: the settings-page matrix read
/// and the sparse override upsert/delete (mirrors
/// <c>ServerNotificationWriteService.SetSettingAsync</c>'s contract exactly).
///
/// <b>Seeded defaults (from EF <c>HasData</c>, survives Respawn):</b> only
/// <c>SearchPage/AutoTreeSearch/AlsoFavorited/AlsoRecommended × Ignored = true</c> exist as system
/// defaults — every other (mode, key) pair has no row, so "system default" is implicitly false.
///
/// <b>Per-test seeding plan:</b> every test seeds its own user via <see cref="IntegrationTestBase.SeedUserAsync"/>;
/// override rows for pre-diverged-state tests are inserted directly via <see cref="ApplicationDbContext"/>
/// (mirrors <c>DiscoveryDefaultsReadServiceTests.SeedUserOverrideAsync</c>). No FK parents beyond
/// the seeded user and the <c>HasData</c>-seeded <c>SearchMode</c>/<c>UserStoryInteractionFilterType</c>
/// catalogs (survive Respawn as seeded lookups).
///
/// Tier: Integration (Testcontainers Postgres, real EF, real Respawn).
/// </summary>
[Collection("Postgres")]
public class DiscoveryFilterSettingsServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    // ── GetMyMatrixAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMyMatrixAsync_NoOverrides_ReturnsSystemDefaultsOnly()
    {
        int userId = await SeedUserAsync();
        SetActiveUser(userId);

        IReadOnlyList<DiscoveryFilterModeDto> matrix = await InvokeAsync(svc => svc.GetMyMatrixAsync());

        matrix.Should().HaveCount(4, "only the four confirmed-consumer search modes are included");
        matrix.Select(m => m.SearchModeKey).Should().BeEquivalentTo(
        [
            SiteSearchModes.SearchPage, SiteSearchModes.AutoTreeSearch,
            SiteSearchModes.AlsoFavorited, SiteSearchModes.AlsoRecommended
        ]);

        DiscoveryFilterModeDto searchPage = matrix.Single(m => m.SearchModeKey == SiteSearchModes.SearchPage);
        searchPage.Rows.Should().HaveCount(6, "the six mappable filter keys — HasStarted has no enum counterpart and is excluded");
        searchPage.Rows.Select(r => r.FilterKey).Should().NotContain(UserStoryInteractionFilters.HasStarted);

        DiscoveryFilterRowDto ignoredRow = searchPage.Rows.Single(r => r.FilterKey == UserStoryInteractionFilters.Ignored);
        ignoredRow.SystemDefault.Should().BeTrue("Ignored=true is the seeded default for SearchPage");
        ignoredRow.EffectiveValue.Should().BeTrue();
        ignoredRow.IsOverridden.Should().BeFalse();

        DiscoveryFilterRowDto favoritedRow = searchPage.Rows.Single(r => r.FilterKey == UserStoryInteractionFilters.Favorited);
        favoritedRow.SystemDefault.Should().BeFalse("no seeded row exists for Favorited on any mode");
        favoritedRow.EffectiveValue.Should().BeFalse();
        favoritedRow.IsOverridden.Should().BeFalse();
    }

    [Fact]
    public async Task GetMyMatrixAsync_WithOverride_ReflectsOverrideNotDefault()
    {
        int userId = await SeedUserAsync();
        await SeedOverrideRowAsync(userId, SiteSearchModes.SearchPage, UserStoryInteractionFilters.Ignored, isEnabled: false);
        SetActiveUser(userId);

        IReadOnlyList<DiscoveryFilterModeDto> matrix = await InvokeAsync(svc => svc.GetMyMatrixAsync());

        DiscoveryFilterRowDto row = matrix
            .Single(m => m.SearchModeKey == SiteSearchModes.SearchPage).Rows
            .Single(r => r.FilterKey == UserStoryInteractionFilters.Ignored);

        row.SystemDefault.Should().BeTrue();
        row.EffectiveValue.Should().BeFalse("the override disables what the system default enables");
        row.IsOverridden.Should().BeTrue();
    }

    [Fact]
    public async Task GetMyMatrixAsync_Unauthenticated_Throws()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());

        Func<Task> act = async () => await InvokeAsync(svc => svc.GetMyMatrixAsync());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── SetOverrideAsync — sparse upsert/delete ──────────────────────────────────────

    [Fact]
    public async Task SetOverrideAsync_DivergesFromDefault_InsertsRow()
    {
        int userId = await SeedUserAsync();
        SetActiveUser(userId);

        await InvokeAsync(svc => svc.SetOverrideAsync(
            SiteSearchModes.SearchPage, UserStoryInteractionFilters.Favorited, isEnabled: true));

        UserStoryInteractionFilterSetting? row = await FindOverrideRowAsync(
            userId, SiteSearchModes.SearchPage, UserStoryInteractionFilters.Favorited);
        row.Should().NotBeNull("Favorited's system default is false, so enabling it diverges and must persist a row");
        row!.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task SetOverrideAsync_MatchesDefault_DeletesRowRatherThanStoringTrue()
    {
        int userId = await SeedUserAsync();
        // Start diverged (Ignored disabled), then set it back to the seeded default (true).
        await SeedOverrideRowAsync(userId, SiteSearchModes.SearchPage, UserStoryInteractionFilters.Ignored, isEnabled: false);
        SetActiveUser(userId);

        await InvokeAsync(svc => svc.SetOverrideAsync(
            SiteSearchModes.SearchPage, UserStoryInteractionFilters.Ignored, isEnabled: true));

        UserStoryInteractionFilterSetting? row = await FindOverrideRowAsync(
            userId, SiteSearchModes.SearchPage, UserStoryInteractionFilters.Ignored);
        row.Should().BeNull("matching the system default means the override row is deleted, not stored as true");
    }

    [Fact]
    public async Task SetOverrideAsync_Idempotent_CallingTwiceLeavesOneRow()
    {
        int userId = await SeedUserAsync();
        SetActiveUser(userId);

        await InvokeAsync(svc => svc.SetOverrideAsync(
            SiteSearchModes.SearchPage, UserStoryInteractionFilters.Completed, isEnabled: true));
        await InvokeAsync(svc => svc.SetOverrideAsync(
            SiteSearchModes.SearchPage, UserStoryInteractionFilters.Completed, isEnabled: true));

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        int count = await db.UserStoryInteractionFilterSettings.CountAsync(s =>
            s.UserId == userId && s.SearchModeKey == SiteSearchModes.SearchPage
            && s.UserStoryInteractionFilterKey == UserStoryInteractionFilters.Completed);
        count.Should().Be(1, "a repeated identical override must upsert, never duplicate");
    }

    [Fact]
    public async Task SetOverrideAsync_Unauthenticated_Throws()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());

        Func<Task> act = async () => await InvokeAsync(svc =>
            svc.SetOverrideAsync(SiteSearchModes.SearchPage, UserStoryInteractionFilters.Ignored, isEnabled: false));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SetOverrideAsync_UnmappableKey_NoOpsRatherThanPersisting()
    {
        int userId = await SeedUserAsync();
        SetActiveUser(userId);

        // HasStarted has no UserStoryInteractionTypeEnum counterpart and isn't panel-exposable —
        // the settings form never sends it, but a direct/malformed call must not crash or persist.
        await InvokeAsync(svc => svc.SetOverrideAsync(
            SiteSearchModes.SearchPage, UserStoryInteractionFilters.HasStarted, isEnabled: true));

        UserStoryInteractionFilterSetting? row = await FindOverrideRowAsync(
            userId, SiteSearchModes.SearchPage, UserStoryInteractionFilters.HasStarted);
        row.Should().BeNull();
    }

    // ── Read/write round-trip ────────────────────────────────────────────────────────

    [Fact]
    public async Task SetOverrideAsync_IsObservedByDiscoveryDefaultsReadService()
    {
        // Closes the loop DiscoveryDefaultsReadServiceTests' SeedUserOverrideAsync test helper only
        // faked directly: a row written through the REAL write path must be picked up by the read
        // merge the four discovery surfaces actually consume.
        int userId = await SeedUserAsync();
        SetActiveUser(userId);

        await InvokeAsync(svc => svc.SetOverrideAsync(
            SiteSearchModes.SearchPage, UserStoryInteractionFilters.Favorited, isEnabled: true));

        using IServiceScope scope = Factory.Services.CreateScope();
        IDiscoveryDefaultsReadService readSvc = scope.ServiceProvider.GetRequiredService<IDiscoveryDefaultsReadService>();
        IReadOnlyList<UserStoryInteractionTypeEnum> result =
            await readSvc.GetDefaultExcludedInteractionsAsync(SiteSearchModes.SearchPage);

        result.Should().Contain(UserStoryInteractionTypeEnum.Favorite);
        result.Should().Contain(UserStoryInteractionTypeEnum.Ignore, "the seeded system default is untouched by the new override");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────

    private async Task<T> InvokeAsync<T>(Func<IDiscoveryFilterSettingsService, Task<T>> call)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IDiscoveryFilterSettingsService svc =
            scope.ServiceProvider.GetRequiredService<IDiscoveryFilterSettingsService>();
        return await call(svc);
    }

    private async Task InvokeAsync(Func<IDiscoveryFilterSettingsService, Task> call)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IDiscoveryFilterSettingsService svc =
            scope.ServiceProvider.GetRequiredService<IDiscoveryFilterSettingsService>();
        await call(svc);
    }

    /// <summary>Inserts one <see cref="UserStoryInteractionFilterSetting"/> row directly (bypasses the service under test).</summary>
    private async Task SeedOverrideRowAsync(int userId, string searchModeKey, string filterKey, bool isEnabled)
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

    private async Task<UserStoryInteractionFilterSetting?> FindOverrideRowAsync(int userId, string searchModeKey, string filterKey)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.UserStoryInteractionFilterSettings.AsNoTracking().FirstOrDefaultAsync(s =>
            s.UserId == userId && s.SearchModeKey == searchModeKey && s.UserStoryInteractionFilterKey == filterKey);
    }
}
