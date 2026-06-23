using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="IUserStoryInteractionWriteService"/> and
/// <see cref="IUserStoryInteractionReadService"/> (WU15). Covers: upsert creates row when absent;
/// updates existing row; date partition stamped on true / nulled on false; sparse cleanup (all-false
/// removes row + cascades date partition); GetStatesByStoryIdsAsync scoped to active user;
/// GetStateAsync returns all-false default when no row; anonymous context returns empty reads and
/// throws on write; IsCompleted panel-managed (HasStarted preserved across write).
///
/// Tier: Integration (real Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class UserStoryInteractionServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private TestAppFactory _factory = null!;
    private int _userId;
    private int _storyId;

    public async Task InitializeAsync()
    {
        _factory = new TestAppFactory(postgres.ConnectionString);
        _ = _factory.Services; // force host build + DataSeeder

        _userId = await GetTestUserIdAsync();
        _storyId = await SeedStoryAsync();

        SetActiveUser(_userId);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    // ── GetStateAsync — default when no row ─────────────────────────────────────

    [Fact]
    public async Task GetStateAsync_WhenNoRow_ReturnsAllFalseDefault()
    {
        int freshStoryId = await SeedStoryAsync();

        UserStoryInteractionStateDto state = await CallGetStateAsync(freshStoryId);

        state.StoryId.Should().Be(freshStoryId);
        state.IsFavorite.Should().BeFalse();
        state.IsHiddenFavorite.Should().BeFalse();
        state.IsFollowed.Should().BeFalse();
        state.IsCompleted.Should().BeFalse();
        state.IsReadItLater.Should().BeFalse();
        state.IsIgnored.Should().BeFalse();
        state.HasStarted.Should().BeFalse();
    }

    // ── SetInteractionStateAsync — creates row ───────────────────────────────────

    [Fact]
    public async Task SetInteractionStateAsync_WhenNoRow_CreatesInteractionRow()
    {
        await CallSetStateAsync(_storyId, new InteractionStateUpdate(
            IsFavorite: true, IsHiddenFavorite: false, IsFollowed: false,
            IsCompleted: false, IsReadItLater: false, IsIgnored: false));

        UserStoryInteraction? row = await GetRowAsync(_userId, _storyId);
        row.Should().NotBeNull("a row must be created when at least one bit is true");
        row!.IsFavorite.Should().BeTrue();
    }

    [Fact]
    public async Task SetInteractionStateAsync_UpdatesExistingRow()
    {
        await CallSetStateAsync(_storyId, new InteractionStateUpdate(
            IsFavorite: true, IsHiddenFavorite: false, IsFollowed: false,
            IsCompleted: false, IsReadItLater: false, IsIgnored: false));

        await CallSetStateAsync(_storyId, new InteractionStateUpdate(
            IsFavorite: false, IsHiddenFavorite: false, IsFollowed: true,
            IsCompleted: false, IsReadItLater: false, IsIgnored: false));

        UserStoryInteraction? row = await GetRowAsync(_userId, _storyId);
        row!.IsFavorite.Should().BeFalse();
        row.IsFollowed.Should().BeTrue();
    }

    // ── Date partition ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SetInteractionStateAsync_WhenBitGoesTrue_StampsFavoriteDate()
    {
        await CallSetStateAsync(_storyId, new InteractionStateUpdate(
            IsFavorite: true, IsHiddenFavorite: false, IsFollowed: false,
            IsCompleted: false, IsReadItLater: false, IsIgnored: false));

        UserStoryInteractionDate? date = await GetDatePartitionAsync(_userId, _storyId);
        date.Should().NotBeNull();
        date!.FavoriteDate.Should().NotBeNull("FavoriteDate must be stamped when IsFavorite goes true");
    }

    [Fact]
    public async Task SetInteractionStateAsync_WhenBitGoesFalse_ClearsFavoriteDate()
    {
        await CallSetStateAsync(_storyId, new InteractionStateUpdate(
            IsFavorite: true, IsHiddenFavorite: false, IsFollowed: false,
            IsCompleted: false, IsReadItLater: false, IsIgnored: false));

        // Now clear IsFavorite but keep IsFollowed so the row doesn't disappear.
        await CallSetStateAsync(_storyId, new InteractionStateUpdate(
            IsFavorite: false, IsHiddenFavorite: false, IsFollowed: true,
            IsCompleted: false, IsReadItLater: false, IsIgnored: false));

        UserStoryInteractionDate? date = await GetDatePartitionAsync(_userId, _storyId);
        date!.FavoriteDate.Should().BeNull("FavoriteDate must be cleared when IsFavorite goes false");
    }

    [Fact]
    public async Task SetInteractionStateAsync_CompletedBitManagedByPanel_StampsCompletedDate()
    {
        await CallSetStateAsync(_storyId, new InteractionStateUpdate(
            IsFavorite: false, IsHiddenFavorite: false, IsFollowed: false,
            IsCompleted: true, IsReadItLater: false, IsIgnored: false));

        UserStoryInteractionDate? date = await GetDatePartitionAsync(_userId, _storyId);
        date!.CompletedDate.Should().NotBeNull("panel can set IsCompleted (read-elsewhere use case)");
    }

    // ── HasStarted preserved ─────────────────────────────────────────────────────

    [Fact]
    public async Task SetInteractionStateAsync_PreservesHasStarted_WhenAlreadySet()
    {
        // Seed HasStarted = true directly (reading path sets this, not the panel).
        await SeedHasStartedAsync(_userId, _storyId);

        await CallSetStateAsync(_storyId, new InteractionStateUpdate(
            IsFavorite: true, IsHiddenFavorite: false, IsFollowed: false,
            IsCompleted: false, IsReadItLater: false, IsIgnored: false));

        UserStoryInteraction? row = await GetRowAsync(_userId, _storyId);
        row!.HasStarted.Should().BeTrue("the write service must never clear HasStarted");
    }

    // ── Sparse cleanup ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SetInteractionStateAsync_WhenAllBitsFalse_RemovesRow()
    {
        await CallSetStateAsync(_storyId, new InteractionStateUpdate(
            IsFavorite: true, IsHiddenFavorite: false, IsFollowed: false,
            IsCompleted: false, IsReadItLater: false, IsIgnored: false));

        await CallSetStateAsync(_storyId, new InteractionStateUpdate(
            IsFavorite: false, IsHiddenFavorite: false, IsFollowed: false,
            IsCompleted: false, IsReadItLater: false, IsIgnored: false));

        UserStoryInteraction? row = await GetRowAsync(_userId, _storyId);
        row.Should().BeNull("row must be deleted when all bits are false");

        UserStoryInteractionDate? date = await GetDatePartitionAsync(_userId, _storyId);
        date.Should().BeNull("date partition must cascade-delete with the interaction row");
    }

    [Fact]
    public async Task SetInteractionStateAsync_WhenAllFalse_WithHasStartedTrue_KeepsRow()
    {
        // HasStarted keeps the row alive even when all panel bits go false.
        await SeedHasStartedAsync(_userId, _storyId);

        await CallSetStateAsync(_storyId, new InteractionStateUpdate(
            IsFavorite: false, IsHiddenFavorite: false, IsFollowed: false,
            IsCompleted: false, IsReadItLater: false, IsIgnored: false));

        UserStoryInteraction? row = await GetRowAsync(_userId, _storyId);
        row.Should().NotBeNull("HasStarted is permanent — the row must survive all-panel-false");
        row!.HasStarted.Should().BeTrue();
    }

    [Fact]
    public async Task SetInteractionStateAsync_WhenAllFalse_AndNoRow_DoesNotCreateRow()
    {
        int freshStoryId = await SeedStoryAsync();

        await CallSetStateAsync(freshStoryId, new InteractionStateUpdate(
            IsFavorite: false, IsHiddenFavorite: false, IsFollowed: false,
            IsCompleted: false, IsReadItLater: false, IsIgnored: false));

        UserStoryInteraction? row = await GetRowAsync(_userId, freshStoryId);
        row.Should().BeNull("no row must be created when all bits are already false");
    }

    // ── GetStatesByStoryIdsAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetStatesByStoryIdsAsync_ReturnsScopedToActiveUser()
    {
        int otherUserId = await CreateThrowawayUserIdAsync();
        int story2 = await SeedStoryAsync();

        // Other user favorited story2; active user favorited _storyId.
        await CallSetStateAsync(_storyId, new InteractionStateUpdate(
            IsFavorite: true, IsHiddenFavorite: false, IsFollowed: false,
            IsCompleted: false, IsReadItLater: false, IsIgnored: false));

        // Directly seed the other user's row so we don't have to swap the fake context.
        await SeedInteractionRowAsync(otherUserId, story2, isFavorite: true);

        IReadOnlyDictionary<int, UserStoryInteractionStateDto> states =
            await CallGetStatesByIdsAsync([_storyId, story2]);

        states.Should().ContainKey(_storyId, "active user interacted with storyId");
        states[_storyId].IsFavorite.Should().BeTrue();
        states.Should().NotContainKey(story2, "story2 was only interacted by another user");
    }

    [Fact]
    public async Task GetStatesByStoryIdsAsync_OmitsMissingRows_CallerTreatsAbsentKeyAsAllFalse()
    {
        int fresh = await SeedStoryAsync();

        IReadOnlyDictionary<int, UserStoryInteractionStateDto> states =
            await CallGetStatesByIdsAsync([fresh]);

        states.Should().NotContainKey(fresh, "missing row is absent from the dictionary");
    }

    // ── Anonymous context ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStateAsync_WhenAnonymous_ReturnsAllFalseWithoutHittingDb()
    {
        SetAnonymous();

        UserStoryInteractionStateDto state = await CallGetStateAsync(_storyId);

        state.IsFavorite.Should().BeFalse();
        state.HasStarted.Should().BeFalse();
    }

    [Fact]
    public async Task GetStatesByStoryIdsAsync_WhenAnonymous_ReturnsEmptyDictionary()
    {
        SetAnonymous();

        IReadOnlyDictionary<int, UserStoryInteractionStateDto> states =
            await CallGetStatesByIdsAsync([_storyId]);

        states.Should().BeEmpty();
    }

    [Fact]
    public async Task SetInteractionStateAsync_WhenAnonymous_Throws()
    {
        SetAnonymous();

        Func<Task> act = () => CallSetStateAsync(_storyId, new InteractionStateUpdate(
            IsFavorite: true, IsHiddenFavorite: false, IsFollowed: false,
            IsCompleted: false, IsReadItLater: false, IsIgnored: false));

        await act.Should().ThrowAsync<InvalidOperationException>("anonymous writes must be rejected");
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    private void SetActiveUser(int userId)
    {
        FakeActiveUserContext fake = _factory.Services.GetRequiredService<FakeActiveUserContext>();
        fake.UserId = userId;
        fake.IsAuthenticated = true;
        fake.ShowMatureContent = true;
    }

    private void SetAnonymous()
    {
        FakeActiveUserContext fake = _factory.Services.GetRequiredService<FakeActiveUserContext>();
        fake.UserId = null;
        fake.IsAuthenticated = false;
    }

    private async Task<int> GetTestUserIdAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Users.Where(u => u.UserName == "TestUser").Select(u => u.Id).FirstAsync();
    }

    private async Task<int> CreateThrowawayUserIdAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<User>>();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        User user = new()
        {
            UserName = $"ThrowawayUSI-{suffix}",
            Email = $"throwaway-usi-{suffix}@test.invalid",
            EmailConfirmed = true,
            ThemeId = 1
        };
        await userManager.CreateAsync(user, "Password123!");
        return user.Id;
    }

    private async Task<int> SeedStoryAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        Story story = new()
        {
            Rating = Rating.E,
            StoryStatusId = StoryStatusEnum.InProgress,
            PublishedDate = DateTime.UtcNow,
            LastUpdatedDate = DateTime.UtcNow,
            StoryListing = new StoryListing { StoryTitle = $"USI Fixture {suffix}", ShortDescription = "test" },
            StoryDetail = new StoryDetail { LongDescription = "test", PostApprovalStatus = StoryStatusEnum.InProgress }
        };
        db.Stories.Add(story);
        await db.SaveChangesAsync();
        return story.StoryId;
    }

    private async Task SeedHasStartedAsync(int userId, int storyId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        UserStoryInteraction? existing = await db.UserStoryInteractions
            .FirstOrDefaultAsync(i => i.UserId == userId && i.StoryId == storyId);
        if (existing is not null)
        {
            existing.HasStarted = true;
        }
        else
        {
            db.UserStoryInteractions.Add(new UserStoryInteraction
                { UserId = userId, StoryId = storyId, HasStarted = true });
        }
        await db.SaveChangesAsync();
    }

    private async Task SeedInteractionRowAsync(int userId, int storyId, bool isFavorite)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.UserStoryInteractions.Add(new UserStoryInteraction
            { UserId = userId, StoryId = storyId, IsFavorite = isFavorite });
        await db.SaveChangesAsync();
    }

    private async Task CallSetStateAsync(int storyId, InteractionStateUpdate update)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IUserStoryInteractionWriteService svc =
            scope.ServiceProvider.GetRequiredService<IUserStoryInteractionWriteService>();
        await svc.SetInteractionStateAsync(storyId, update);
    }

    private async Task<UserStoryInteractionStateDto> CallGetStateAsync(int storyId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IUserStoryInteractionReadService svc =
            scope.ServiceProvider.GetRequiredService<IUserStoryInteractionReadService>();
        return await svc.GetStateAsync(storyId);
    }

    private async Task<IReadOnlyDictionary<int, UserStoryInteractionStateDto>> CallGetStatesByIdsAsync(
        IReadOnlyList<int> storyIds)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IUserStoryInteractionReadService svc =
            scope.ServiceProvider.GetRequiredService<IUserStoryInteractionReadService>();
        return await svc.GetStatesByStoryIdsAsync(storyIds);
    }

    private async Task<UserStoryInteraction?> GetRowAsync(int userId, int storyId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.UserStoryInteractions
            .FirstOrDefaultAsync(i => i.UserId == userId && i.StoryId == storyId);
    }

    private async Task<UserStoryInteractionDate?> GetDatePartitionAsync(int userId, int storyId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.UserStoryInteractionDates
            .FirstOrDefaultAsync(d => d.UserId == userId && d.StoryId == storyId);
    }
}
