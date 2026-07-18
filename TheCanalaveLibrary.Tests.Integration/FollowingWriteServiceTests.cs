using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="IFollowingWriteService"/> (WU21). Covers the core behavioural
/// contracts of the Following/Vouches write path against a real Postgres container.
///
/// <b>What's tested:</b> follow/unfollow idempotency; self-follow guard; bell toggle;
/// vouch with and without text; VouchText sanitization (script stripping, allowed formatting kept);
/// 5-vouch limit (VouchLimitException on 6th vouch); remove vouch frees a slot; duplicate vouch
/// idempotency.
///
/// <b>Isolation:</b> each test seeds its own throwaway Guid-suffixed users and cleans up via natural
/// cascade when the Testcontainers container is torn down. The DataSeeder's TestUser/AdminUser rows
/// are never touched.
///
/// Tier: <b>Integration</b> (real Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class FollowingWriteServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _actorId;
    private int _targetId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _actorId = await SeedUserAsync();
        _targetId = await SeedUserAsync();
        SetActiveUser(_actorId);
    }

    // ── FollowAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task FollowAsync_CreatesFollowedUserRow()
    {
        await CallFollowAsync(_targetId);

        bool exists = await AnyFollowedUserAsync(_actorId, _targetId);
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task FollowAsync_SetsReceiveAlertsTrue_ByDefault()
    {
        await CallFollowAsync(_targetId);

        bool alerts = await GetReceiveAlertsAsync(_actorId, _targetId);
        alerts.Should().BeTrue("new follows default to receive-alerts on");
    }

    [Fact]
    public async Task FollowAsync_Idempotent_WhenAlreadyFollowing()
    {
        await CallFollowAsync(_targetId);
        await CallFollowAsync(_targetId); // second call — must not throw or insert a duplicate

        int count = await CountFollowedUserRowsAsync(_actorId, _targetId);
        count.Should().Be(1);
    }

    [Fact]
    public async Task FollowAsync_SelfFollow_Throws()
    {
        Func<Task> act = () => CallFollowAsync(_actorId);
        await act.Should().ThrowAsync<FollowingValidationException>("self-follow must be rejected");
    }

    // ── UnfollowAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task UnfollowAsync_RemovesFollowedUserRow()
    {
        await CallFollowAsync(_targetId);
        await CallUnfollowAsync(_targetId);

        bool exists = await AnyFollowedUserAsync(_actorId, _targetId);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task UnfollowAsync_Idempotent_WhenNotFollowing()
    {
        Func<Task> act = () => CallUnfollowAsync(_targetId);
        await act.Should().NotThrowAsync("unfollowing a non-followed user is a no-op");
    }

    // ── SetReceiveAlertsAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task SetReceiveAlertsAsync_UpdatesFlag()
    {
        await CallFollowAsync(_targetId);
        await CallSetReceiveAlertsAsync(_targetId, false);

        bool alerts = await GetReceiveAlertsAsync(_actorId, _targetId);
        alerts.Should().BeFalse();
    }

    [Fact]
    public async Task SetReceiveAlertsAsync_ThrowsWhenNotFollowing()
    {
        Func<Task> act = () => CallSetReceiveAlertsAsync(_targetId, true);
        await act.Should().ThrowAsync<FollowingValidationException>();
    }

    // ── VouchAsync ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task VouchAsync_WithNullText_CreatesVouchRow()
    {
        await CallVouchAsync(_targetId, null);

        bool exists = await AnyVouchAsync(_actorId, _targetId);
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task VouchAsync_WithRichText_SanitizesAndPersists()
    {
        const string malicious = "<p>Good endorsement <script>alert(1)</script><strong>text</strong></p>";
        await CallVouchAsync(_targetId, malicious);

        string? stored = await GetVouchTextAsync(_actorId, _targetId);
        stored.Should().NotContain("<script>", "script tags must be stripped by the sanitizer");
        stored.Should().Contain("<strong>", "allowed formatting must be preserved");
    }

    [Fact]
    public async Task VouchAsync_WithLongText_PersistsUntruncated()
    {
        // Verify the unbounded column — text well beyond the old MaxLength(1000) must persist.
        string longParagraph = "<p>" + new string('x', 5000) + "</p>";
        await CallVouchAsync(_targetId, longParagraph);

        string? stored = await GetVouchTextAsync(_actorId, _targetId);
        stored.Should().HaveLength(stored!.Length, "unbounded column must store all content");
        stored.Should().Contain(new string('x', 1000), "original content is preserved past 1000 chars");
    }

    [Fact]
    public async Task VouchAsync_SelfVouch_Throws()
    {
        Func<Task> act = () => CallVouchAsync(_actorId, null);
        await act.Should().ThrowAsync<FollowingValidationException>("self-vouch must be rejected");
    }

    [Fact]
    public async Task VouchAsync_Idempotent_WhenAlreadyVouched()
    {
        await CallVouchAsync(_targetId, null);
        await CallVouchAsync(_targetId, "<p>Different note</p>"); // second call — no-op

        int count = await CountVouchRowsAsync(_actorId, _targetId);
        count.Should().Be(1, "duplicate vouch is a no-op, not an insert");
    }

    [Fact]
    public async Task VouchAsync_AtFiveVouches_ThrowsVouchLimitException()
    {
        // Fill the actor's 5 slots with 5 different targets.
        int[] targets = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => SeedUserAsync()));
        foreach (int t in targets)
        {
            await CallVouchAsync(t, null);
        }

        // The 6th target must trigger the limit.
        int sixthTarget = await SeedUserAsync();
        Func<Task> act = () => CallVouchAsync(sixthTarget, null);
        await act.Should().ThrowAsync<VouchLimitException>("the 6th vouch must be rejected");
    }

    [Fact]
    public async Task RemoveVouchAsync_FreesASlot_AllowingNewVouch()
    {
        int[] targets = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => SeedUserAsync()));
        foreach (int t in targets)
        {
            await CallVouchAsync(t, null);
        }

        // Remove one, then the slot should be free.
        await CallRemoveVouchAsync(targets[0]);

        int newTarget = await SeedUserAsync();
        Func<Task> act = () => CallVouchAsync(newTarget, null);
        await act.Should().NotThrowAsync("freeing a slot must allow a new vouch");
    }

    [Fact]
    public async Task RemoveVouchAsync_Idempotent_WhenNoVouch()
    {
        Func<Task> act = () => CallRemoveVouchAsync(_targetId);
        await act.Should().NotThrowAsync("removing a non-existent vouch is a no-op");
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    private async Task CallFollowAsync(int targetUserId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IFollowingWriteService svc = scope.ServiceProvider.GetRequiredService<IFollowingWriteService>();
        await svc.FollowAsync(targetUserId);
    }

    private async Task CallUnfollowAsync(int targetUserId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IFollowingWriteService svc = scope.ServiceProvider.GetRequiredService<IFollowingWriteService>();
        await svc.UnfollowAsync(targetUserId);
    }

    private async Task CallSetReceiveAlertsAsync(int targetUserId, bool value)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IFollowingWriteService svc = scope.ServiceProvider.GetRequiredService<IFollowingWriteService>();
        await svc.SetReceiveAlertsAsync(targetUserId, value);
    }

    private async Task CallVouchAsync(int targetUserId, string? text)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IFollowingWriteService svc = scope.ServiceProvider.GetRequiredService<IFollowingWriteService>();
        await svc.VouchAsync(targetUserId, text);
    }

    private async Task CallRemoveVouchAsync(int targetUserId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IFollowingWriteService svc = scope.ServiceProvider.GetRequiredService<IFollowingWriteService>();
        await svc.RemoveVouchAsync(targetUserId);
    }

    private async Task<bool> AnyFollowedUserAsync(int userId, int followedId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.FollowedUsers.AnyAsync(f => f.UserId == userId && f.FollowedUserId == followedId);
    }

    private async Task<bool> GetReceiveAlertsAsync(int userId, int followedId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.FollowedUsers
            .Where(f => f.UserId == userId && f.FollowedUserId == followedId)
            .Select(f => f.ReceiveAlerts)
            .FirstOrDefaultAsync();
    }

    private async Task<int> CountFollowedUserRowsAsync(int userId, int followedId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.FollowedUsers.CountAsync(f => f.UserId == userId && f.FollowedUserId == followedId);
    }

    private async Task<bool> AnyVouchAsync(int vouchingId, int vouchedId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Vouches.AnyAsync(v => v.VouchingUserId == vouchingId && v.VouchedUserId == vouchedId);
    }

    private async Task<string?> GetVouchTextAsync(int vouchingId, int vouchedId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Vouches
            .Where(v => v.VouchingUserId == vouchingId && v.VouchedUserId == vouchedId)
            .Select(v => v.VouchText)
            .FirstOrDefaultAsync();
    }

    private async Task<int> CountVouchRowsAsync(int vouchingId, int vouchedId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Vouches.CountAsync(v => v.VouchingUserId == vouchingId && v.VouchedUserId == vouchedId);
    }
}
