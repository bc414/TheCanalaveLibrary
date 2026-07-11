using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for the user-activity signal buffer's flush path (WU-SiteDailyStat, Feature
/// 62 L2): buffered pings land as one batched upsert into <c>User.LastActiveUtc</c>. The timer
/// worker is removed by <see cref="TestAppFactory"/>; flushes here are deterministic (testing.md).
/// Tier: Integration (Testcontainers Postgres).
/// </summary>
[Collection("Postgres")]
public class UserActivityFlushTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _userId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _userId = await SeedUserAsync();
    }

    [Fact]
    public async Task RecordActivity_DoesNotTouchTheDb_UntilFlush()
    {
        await RecordActivityAsync(_userId);

        (await LoadLastActiveUtcAsync(_userId)).Should().BeNull(
            "activity pings are buffered in-process; nothing persists before a flush");

        int written = await FlushAsync();
        written.Should().Be(1);

        (await LoadLastActiveUtcAsync(_userId)).Should().NotBeNull();
    }

    [Fact]
    public async Task Flush_NeverRegressesAnEarlierStamp()
    {
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            DateTime future = DateTime.UtcNow.AddDays(1);
            await db.Database.ExecuteSqlAsync($"UPDATE \"AspNetUsers\" SET last_active_utc = {future} WHERE id = {_userId}");
        }

        await RecordActivityAsync(_userId); // "now" — earlier than the seeded future stamp
        await FlushAsync();

        DateTime? lastActive = await LoadLastActiveUtcAsync(_userId);
        lastActive.Should().BeCloseTo(DateTime.UtcNow.AddDays(1), TimeSpan.FromSeconds(5),
            "GREATEST keeps the later stamp — a stale ping must never regress last_active_utc");
    }

    [Fact]
    public async Task PingForDeletedUser_DoesNotThrow()
    {
        int doomedUserId = await SeedUserAsync();
        await RecordActivityAsync(doomedUserId);

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"AspNetUsers\" WHERE id = {doomedUserId}");
        }

        Func<Task> flush = () => FlushAsync();
        await flush.Should().NotThrowAsync("the UPDATE ... FROM naturally no-ops for a missing user_id");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────

    private async Task RecordActivityAsync(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IUserActivityWriteService svc = scope.ServiceProvider.GetRequiredService<IUserActivityWriteService>();
        await svc.RecordActivityAsync(userId);
    }

    private Task<int> FlushAsync() =>
        Factory.Services.GetRequiredService<UserActivityFlusher>().FlushAsync();

    private async Task<DateTime?> LoadLastActiveUtcAsync(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Users.Where(u => u.Id == userId).Select(u => u.LastActiveUtc).SingleAsync();
    }
}
