using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="IFollowingReadService"/> (WU21). Covers the read-side
/// contracts: <c>GetRelationshipStateAsync</c> correctness; <c>GetFollowedUsersAsync</c> projection
/// (UserCardDto, avatar default fallback); <c>GetOutgoingVouchesAsync</c> (public — any caller);
/// <c>GetIncomingVouchesAsync</c> (private — scoped to the active user only, §5.8 asymmetry).
///
/// Tier: <b>Integration</b> (real Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class FollowingReadServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _viewerId;
    private int _targetId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _viewerId = await SeedUserAsync();
        _targetId = await SeedUserAsync();
        SetActiveUser(_viewerId);
    }

    // ── GetRelationshipStateAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetRelationshipStateAsync_NoRelationship_ReturnsAllFalseAndZeroCount()
    {
        UserRelationshipStateDto state = await CallGetRelationshipStateAsync(_targetId);

        state.IsFollowing.Should().BeFalse();
        state.ReceiveAlerts.Should().BeFalse();
        state.IsVouched.Should().BeFalse();
        state.OutgoingVouchCount.Should().Be(0);
    }

    [Fact]
    public async Task GetRelationshipStateAsync_WhenFollowing_ReturnsIsFollowingTrue()
    {
        await SeedFollowAsync(_viewerId, _targetId);

        UserRelationshipStateDto state = await CallGetRelationshipStateAsync(_targetId);

        state.IsFollowing.Should().BeTrue();
        state.ReceiveAlerts.Should().BeTrue("default is receive-alerts on");
    }

    [Fact]
    public async Task GetRelationshipStateAsync_WhenVouched_ReturnsIsVouchedTrueAndCountOne()
    {
        await SeedVouchAsync(_viewerId, _targetId, null);

        UserRelationshipStateDto state = await CallGetRelationshipStateAsync(_targetId);

        state.IsVouched.Should().BeTrue();
        state.OutgoingVouchCount.Should().Be(1);
    }

    [Fact]
    public async Task GetRelationshipStateAsync_Anonymous_ReturnsZeroState()
    {
        Factory.Services.GetRequiredService<FakeActiveUserContext>().UserId = null;
        Factory.Services.GetRequiredService<FakeActiveUserContext>().IsAuthenticated = false;

        UserRelationshipStateDto state = await CallGetRelationshipStateAsync(_targetId);

        state.IsFollowing.Should().BeFalse();
        state.OutgoingVouchCount.Should().Be(0);
    }

    // ── GetFollowedUsersAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetFollowedUsersAsync_ReturnsUserCardDtoForEachFollow()
    {
        int followedId = await SeedUserAsync();
        await SeedFollowAsync(_viewerId, followedId);

        IReadOnlyList<UserCardDto> cards = await CallGetFollowedUsersAsync(_viewerId);

        cards.Should().ContainSingle(c => c.UserId == followedId);
    }

    [Fact]
    public async Task GetFollowedUsersAsync_UsesDefaultAvatar_WhenNoPictureSet()
    {
        // Throwaway users have no ProfilePictureRelativeUrl (null) — the service must substitute the default.
        await SeedFollowAsync(_viewerId, _targetId);

        IReadOnlyList<UserCardDto> cards = await CallGetFollowedUsersAsync(_viewerId);

        UserCardDto card = cards.Should().ContainSingle(c => c.UserId == _targetId).Subject;
        card.AvatarUrl.Should().Be("/img/default-avatar.svg",
            "null ProfilePictureRelativeUrl must map to the default avatar path");
    }

    [Fact]
    public async Task GetFollowedUsersAsync_EmptyList_WhenNoFollows()
    {
        IReadOnlyList<UserCardDto> cards = await CallGetFollowedUsersAsync(_viewerId);
        cards.Should().BeEmpty();
    }

    // ── GetOutgoingVouchesAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetOutgoingVouchesAsync_ReturnsVouchWithText()
    {
        await SeedVouchAsync(_viewerId, _targetId, "<p>Great contributor.</p>");

        IReadOnlyList<VouchDisplayDto> vouches = await CallGetOutgoingVouchesAsync(_viewerId);

        VouchDisplayDto vouch = vouches.Should().ContainSingle(v => v.User.UserId == _targetId).Subject;
        vouch.VouchText.Should().Be("<p>Great contributor.</p>");
    }

    [Fact]
    public async Task GetOutgoingVouchesAsync_IsPublic_CanQueryAnotherUsersVouches()
    {
        int otherVoucher = await SeedUserAsync();
        await SeedVouchAsync(otherVoucher, _targetId, null);

        // Active user is _viewerId, but we query otherVoucher's outgoing vouches — should succeed.
        IReadOnlyList<VouchDisplayDto> vouches = await CallGetOutgoingVouchesAsync(otherVoucher);
        vouches.Should().ContainSingle(v => v.User.UserId == _targetId);
    }

    // ── GetIncomingVouchesAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetIncomingVouchesAsync_ReturnsVouchesForCurrentUser()
    {
        int voucherA = await SeedUserAsync();
        int voucherB = await SeedUserAsync();

        await SeedVouchAsync(voucherA, _viewerId, null);
        await SeedVouchAsync(voucherB, _viewerId, null);

        // Active user is _viewerId — incoming vouches are the two we just seeded.
        IReadOnlyList<VouchDisplayDto> incoming = await CallGetIncomingVouchesAsync();
        incoming.Select(v => v.User.UserId).Should()
            .BeEquivalentTo(new[] { voucherA, voucherB },
                "incoming vouches are scoped to the active user");
    }

    [Fact]
    public async Task GetIncomingVouchesAsync_DoesNotRevealOtherUsersIncomingVouches()
    {
        // Seed a vouch targeted at _targetId (not the active user _viewerId).
        int voucherX = await SeedUserAsync();
        await SeedVouchAsync(voucherX, _targetId, null);

        // Active user is _viewerId — should see 0 incoming vouches (the seeded one targets _targetId).
        IReadOnlyList<VouchDisplayDto> incoming = await CallGetIncomingVouchesAsync();
        incoming.Should().NotContain(v => v.User.UserId == voucherX,
            "incoming vouches scoped to the active user must not reveal other users' received vouches");
    }

    [Fact]
    public async Task GetIncomingVouchesAsync_Anonymous_ReturnsEmpty()
    {
        Factory.Services.GetRequiredService<FakeActiveUserContext>().UserId = null;
        Factory.Services.GetRequiredService<FakeActiveUserContext>().IsAuthenticated = false;

        IReadOnlyList<VouchDisplayDto> incoming = await CallGetIncomingVouchesAsync();
        incoming.Should().BeEmpty("anonymous callers have no incoming vouches to show");
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    private async Task SeedFollowAsync(int userId, int followedId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        bool exists = await db.FollowedUsers.AnyAsync(f => f.UserId == userId && f.FollowedUserId == followedId);
        if (!exists)
        {
            db.FollowedUsers.Add(new FollowedUser
            {
                UserId = userId,
                FollowedUserId = followedId,
                DateFollowed = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
    }

    private async Task SeedVouchAsync(int vouchingId, int vouchedId, string? text)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        bool exists = await db.Vouches.AnyAsync(v => v.VouchingUserId == vouchingId && v.VouchedUserId == vouchedId);
        if (!exists)
        {
            db.Vouches.Add(new Vouch
            {
                VouchingUserId = vouchingId,
                VouchedUserId = vouchedId,
                VouchText = text,
                DateVouched = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
    }

    private async Task<UserRelationshipStateDto> CallGetRelationshipStateAsync(int targetId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IFollowingReadService svc = scope.ServiceProvider.GetRequiredService<IFollowingReadService>();
        return await svc.GetRelationshipStateAsync(targetId);
    }

    private async Task<IReadOnlyList<UserCardDto>> CallGetFollowedUsersAsync(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IFollowingReadService svc = scope.ServiceProvider.GetRequiredService<IFollowingReadService>();
        return await svc.GetFollowedUsersAsync(userId);
    }

    private async Task<IReadOnlyList<VouchDisplayDto>> CallGetOutgoingVouchesAsync(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IFollowingReadService svc = scope.ServiceProvider.GetRequiredService<IFollowingReadService>();
        return await svc.GetOutgoingVouchesAsync(userId);
    }

    private async Task<IReadOnlyList<VouchDisplayDto>> CallGetIncomingVouchesAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IFollowingReadService svc = scope.ServiceProvider.GetRequiredService<IFollowingReadService>();
        return await svc.GetIncomingVouchesAsync();
    }
}
