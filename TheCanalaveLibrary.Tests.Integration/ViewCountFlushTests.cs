using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for the view-count signal buffer's flush path (Feature 45 L2): buffered view
/// pings land as one batched upsert into <c>daily_story_stats</c> (per-story/day accumulation;
/// migration-managed raw DDL, no EF model), and the lifetime total surfaces through
/// <see cref="IStoryReadService.GetStoryTotalViewsAsync"/> as SUM over the story's rows. The timer
/// worker is removed by <see cref="TestAppFactory"/>; flushes here are deterministic (testing.md).
/// FK parents per test: story via <see cref="IntegrationTestBase.SeedStoryAsync"/> (the only parent
/// <c>daily_story_stats</c> references). Tier: Integration (Testcontainers Postgres).
/// </summary>
[Collection("Postgres")]
public class ViewCountFlushTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _storyId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _storyId = await SeedStoryAsync();
        // Views count for everyone — run these tests anonymous to prove no auth gate exists.
        SetActiveUser(FakeActiveUserContext.Anonymous());
    }

    [Fact]
    public async Task RecordView_DoesNotTouchTheDb_UntilFlush()
    {
        await RecordViewAsync(_storyId);

        (await LoadTodayViewsAsync(_storyId)).Should().BeNull(
            "views are buffered in-process; nothing persists before a flush");

        int written = await FlushAsync();
        written.Should().Be(1);

        (await LoadTodayViewsAsync(_storyId)).Should().Be(1);
    }

    [Fact]
    public async Task MultipleViews_CoalesceIntoTodaysRow()
    {
        await RecordViewAsync(_storyId);
        await RecordViewAsync(_storyId);
        await RecordViewAsync(_storyId);

        int written = await FlushAsync();

        written.Should().Be(1, "three views of one story coalesce to one row-write");
        (await LoadTodayViewsAsync(_storyId)).Should().Be(3);
    }

    [Fact]
    public async Task SecondFlushSameDay_AccumulatesOntoTheSameRow()
    {
        await RecordViewAsync(_storyId);
        await FlushAsync();
        await RecordViewAsync(_storyId);
        await RecordViewAsync(_storyId);
        await FlushAsync();

        (await LoadTodayViewsAsync(_storyId)).Should().Be(3, "ON CONFLICT adds onto today's row");
        (await CountRowsAsync(_storyId)).Should().Be(1, "same UTC day → one row");
    }

    [Fact]
    public async Task TotalViews_SumsAcrossDays()
    {
        // Yesterday's history — inserted directly (the accumulation table is plain SQL).
        await ExecuteSqlAsync(
            $"INSERT INTO daily_story_stats (story_id, stat_date, view_count) " +
            $"VALUES ({_storyId}, (CURRENT_DATE - INTERVAL '1 day')::date, 40)");

        await RecordViewAsync(_storyId);
        await RecordViewAsync(_storyId);
        await FlushAsync();

        long total = await GetTotalViewsAsync(_storyId);
        total.Should().Be(42, "lifetime total = SUM over the story's daily rows");
    }

    [Fact]
    public async Task TotalViews_ZeroForNeverViewedStory()
    {
        (await GetTotalViewsAsync(_storyId)).Should().Be(0);
    }

    [Fact]
    public async Task ViewsOfDeletedStory_AreDropped_WithoutPoisoningTheBatch()
    {
        int doomedStoryId = await SeedStoryAsync();
        await RecordViewAsync(doomedStoryId);
        await RecordViewAsync(_storyId);

        await ExecuteSqlAsync($"DELETE FROM stories WHERE story_id = {doomedStoryId}");

        Func<Task> flush = FlushAsync;
        await flush.Should().NotThrowAsync("a stale view must not FK-fail the whole batch");

        (await LoadTodayViewsAsync(_storyId)).Should().Be(1);
        (await CountRowsAsync(doomedStoryId)).Should().Be(0);
    }

    [Fact]
    public async Task DeletingAStory_CascadesItsStatRows()
    {
        await RecordViewAsync(_storyId);
        await FlushAsync();
        (await CountRowsAsync(_storyId)).Should().Be(1);

        await ExecuteSqlAsync($"DELETE FROM stories WHERE story_id = {_storyId}");

        (await CountRowsAsync(_storyId)).Should().Be(0, "FK ON DELETE CASCADE cleans stat history");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────

    private async Task RecordViewAsync(int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IViewCountWriteService svc = scope.ServiceProvider.GetRequiredService<IViewCountWriteService>();
        await svc.RecordViewAsync(storyId);
    }

    private Task<int> FlushAsync() =>
        Factory.Services.GetRequiredService<ViewCountFlusher>().FlushAsync();

    private async Task<long> GetTotalViewsAsync(int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryReadService svc = scope.ServiceProvider.GetRequiredService<IStoryReadService>();
        return await svc.GetStoryTotalViewsAsync(storyId);
    }

    /// <summary>Today's (UTC) view_count for the story, or null when no row exists yet.</summary>
    private async Task<int?> LoadTodayViewsAsync(int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Database
            .SqlQuery<int?>($"""
                SELECT view_count AS "Value" FROM daily_story_stats
                WHERE story_id = {storyId} AND stat_date = CURRENT_DATE
                """)
            .FirstOrDefaultAsync();
    }

    private async Task<int> CountRowsAsync(int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Database
            .SqlQuery<int>($"""
                SELECT COUNT(*)::int AS "Value" FROM daily_story_stats WHERE story_id = {storyId}
                """)
            .SingleAsync();
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.ExecuteSqlRawAsync(sql);
    }
}
