using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="IStoryAcknowledgmentWriteService"/> /
/// <see cref="IStoryAcknowledgmentReadService"/> (WU-StatBadgeProducers). Covers: request/accept/
/// decline/revoke lifecycle, self-credit rejection, ownership/recipient-identity gating, re-request-
/// after-decline row reuse, the <c>AcknowledgedAsBetaReaderCount</c> producer's increment (Accept)
/// and decrement (Revoke-while-Accepted, transition-delta) paths, the <c>BetaReader</c> badge
/// auto-award at ≥1 with <c>EarnedCount</c> tracking, and the public display filter (Accepted only).
///
/// <b>Per-test seeding:</b> every test seeds users and stories via <c>SeedUserAsync</c> /
/// <c>SeedStoryAsync</c>; Respawn resets the DB between every test — see testing.md. Role/lookup
/// rows (<c>acknowledgment_roles</c>) survive Respawn (TablesToIgnore) — no inline seeding needed.
///
/// Tier: Integration (real Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class StoryAcknowledgmentServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private const short BetaReaderRoleId = 1;
    private const short PlannerRoleId = 2;

    private int _authorId;
    private int _recipientId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _authorId    = await SeedUserAsync("author");
        _recipientId = await SeedUserAsync("recipient");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // RequestAcknowledgmentAsync
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RequestAcknowledgment_Author_CreatesPendingAndNotifiesRecipient()
    {
        int storyId = await SeedStoryAsync(authorId: _authorId);

        SetActiveUser(_authorId);
        await RequestAsync(storyId, _recipientId, BetaReaderRoleId);

        StoryAcknowledgment? row = await GetRawAsync(storyId, _recipientId, BetaReaderRoleId);
        row.Should().NotBeNull();
        row!.StatusId.Should().Be(StoryAcknowledgmentStatus.Pending);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        bool notified = await db.Notifications.AnyAsync(n =>
            n.RecipientUserId == _recipientId
            && n.NotificationTypeId == NotificationTypeEnum.NewStoryAcknowledgement
            && n.SourceUserId == _authorId
            && n.RelatedEntityId == storyId);
        notified.Should().BeTrue("the credited user should be notified");
    }

    [Fact]
    public async Task RequestAcknowledgment_NotStoryOwner_ThrowsUnauthorizedAccess()
    {
        int storyId = await SeedStoryAsync(authorId: _recipientId); // not the caller's story

        SetActiveUser(_authorId);
        Func<Task> act = () => RequestAsync(storyId, _recipientId, BetaReaderRoleId);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RequestAcknowledgment_SelfCredit_ThrowsValidation()
    {
        int storyId = await SeedStoryAsync(authorId: _authorId);

        SetActiveUser(_authorId);
        Func<Task> act = () => RequestAsync(storyId, _authorId, BetaReaderRoleId);
        await act.Should().ThrowAsync<StoryAcknowledgmentValidationException>(
            "crediting yourself would let a single account mint its own beta-reader count");
    }

    [Fact]
    public async Task RequestAcknowledgment_UnknownRecipient_ThrowsValidation()
    {
        int storyId = await SeedStoryAsync(authorId: _authorId);

        SetActiveUser(_authorId);
        Func<Task> act = () => RequestAsync(storyId, 999_999_999, BetaReaderRoleId);
        await act.Should().ThrowAsync<StoryAcknowledgmentValidationException>();
    }

    [Fact]
    public async Task RequestAcknowledgment_UnknownRole_ThrowsValidation()
    {
        int storyId = await SeedStoryAsync(authorId: _authorId);

        SetActiveUser(_authorId);
        Func<Task> act = () => RequestAsync(storyId, _recipientId, roleId: 999);
        await act.Should().ThrowAsync<StoryAcknowledgmentValidationException>();
    }

    [Fact]
    public async Task RequestAcknowledgment_DuplicateActive_ThrowsValidation()
    {
        int storyId = await SeedStoryAsync(authorId: _authorId);

        SetActiveUser(_authorId);
        await RequestAsync(storyId, _recipientId, BetaReaderRoleId);

        Func<Task> act = () => RequestAsync(storyId, _recipientId, BetaReaderRoleId);
        await act.Should().ThrowAsync<StoryAcknowledgmentValidationException>();
    }

    [Fact]
    public async Task RequestAcknowledgment_ReRequestAfterDecline_ReusesRowAsPending()
    {
        int storyId = await SeedStoryAsync(authorId: _authorId);

        SetActiveUser(_authorId);
        await RequestAsync(storyId, _recipientId, PlannerRoleId);

        SetActiveUser(_recipientId);
        await DeclineAsync(storyId, PlannerRoleId);

        SetActiveUser(_authorId);
        await RequestAsync(storyId, _recipientId, PlannerRoleId); // re-request, same triple

        StoryAcknowledgment? row = await GetRawAsync(storyId, _recipientId, PlannerRoleId);
        row.Should().NotBeNull("composite PK reused, not duplicate-inserted");
        row!.StatusId.Should().Be(StoryAcknowledgmentStatus.Pending);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        int rowCount = await db.StoryAcknowledgments.CountAsync(a =>
            a.StoryId == storyId && a.AcknowledgedUserId == _recipientId && a.AcknowledgmentRoleId == PlannerRoleId);
        rowCount.Should().Be(1);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // AcceptAsync — the AcknowledgedAsBetaReaderCount + BetaReader badge producer
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Accept_BetaReaderRole_FlipsToAcceptedAndIncrementsCounter()
    {
        // The same-transaction ExecuteUpdateAsync producer is a silent no-op when the recipient
        // has no UserStat row yet (SeedUserAsync never creates one — no production write path
        // does either; UserStatRecalculator is the real first populator). Integration tests must
        // seed one explicitly, matching RecommendationWriteServiceTests' SeedUserStatAsync.
        await SeedUserStatAsync(_recipientId);
        int storyId = await SeedStoryAsync(authorId: _authorId);
        SetActiveUser(_authorId);
        await RequestAsync(storyId, _recipientId, BetaReaderRoleId);

        SetActiveUser(_recipientId);
        await AcceptAsync(storyId, BetaReaderRoleId);

        StoryAcknowledgment? row = await GetRawAsync(storyId, _recipientId, BetaReaderRoleId);
        row!.StatusId.Should().Be(StoryAcknowledgmentStatus.Accepted);
        row.DateResponded.Should().NotBeNull();

        (await LoadCounterAsync(_recipientId)).Should().Be(1);
    }

    [Fact]
    public async Task Accept_NonBetaReaderRole_FlipsToAcceptedButDoesNotIncrementCounter()
    {
        int storyId = await SeedStoryAsync(authorId: _authorId);
        SetActiveUser(_authorId);
        await RequestAsync(storyId, _recipientId, PlannerRoleId);

        SetActiveUser(_recipientId);
        await AcceptAsync(storyId, PlannerRoleId);

        StoryAcknowledgment? row = await GetRawAsync(storyId, _recipientId, PlannerRoleId);
        row!.StatusId.Should().Be(StoryAcknowledgmentStatus.Accepted);
        (await LoadCounterAsync(_recipientId)).Should().Be(0, "only the Beta Reader role feeds this counter");
    }

    [Fact]
    public async Task Accept_AtFirstAcceptance_AwardsBetaReaderBadgeWithEarnedCountOne()
    {
        await SeedUserStatAsync(_recipientId);
        int storyId = await SeedStoryAsync(authorId: _authorId);
        SetActiveUser(_authorId);
        await RequestAsync(storyId, _recipientId, BetaReaderRoleId);

        SetActiveUser(_recipientId);
        await AcceptAsync(storyId, BetaReaderRoleId);

        UserBadge? badge = await LoadBadgeAsync(_recipientId, SiteBadges.BetaReader);
        badge.Should().NotBeNull("no-tiers model: the badge auto-awards at the FIRST accepted credit");
        badge!.EarnedCount.Should().Be(1);
    }

    [Fact]
    public async Task Accept_SecondCredit_UpdatesEarnedCountWithoutDuplicateBadgeRow()
    {
        await SeedUserStatAsync(_recipientId);
        int storyOne = await SeedStoryAsync(authorId: _authorId);
        int storyTwo = await SeedStoryAsync(authorId: _authorId);
        SetActiveUser(_authorId);
        await RequestAsync(storyOne, _recipientId, BetaReaderRoleId);
        await RequestAsync(storyTwo, _recipientId, BetaReaderRoleId);

        SetActiveUser(_recipientId);
        await AcceptAsync(storyOne, BetaReaderRoleId);
        await AcceptAsync(storyTwo, BetaReaderRoleId);

        (await LoadCounterAsync(_recipientId)).Should().Be(2);
        UserBadge? badge = await LoadBadgeAsync(_recipientId, SiteBadges.BetaReader);
        badge!.EarnedCount.Should().Be(2, "mutation-sanity: a call that fails to update EarnedCount on repeat award must fail this test");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        int badgeRowCount = await db.UserBadges.CountAsync(ub =>
            ub.UserId == _recipientId && ub.BadgeKey == SiteBadges.BetaReader);
        badgeRowCount.Should().Be(1, "idempotent award — never a second row for the same user+badge");
    }

    [Fact]
    public async Task Accept_NotRecipient_ThrowsKeyNotFound()
    {
        int storyId = await SeedStoryAsync(authorId: _authorId);
        SetActiveUser(_authorId);
        await RequestAsync(storyId, _recipientId, BetaReaderRoleId);

        // Still the author — not the credited recipient, so no row keyed to their id exists.
        Func<Task> act = () => AcceptAsync(storyId, BetaReaderRoleId);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Accept_AlreadyAccepted_ThrowsValidation()
    {
        int storyId = await SeedStoryAsync(authorId: _authorId);
        SetActiveUser(_authorId);
        await RequestAsync(storyId, _recipientId, BetaReaderRoleId);

        SetActiveUser(_recipientId);
        await AcceptAsync(storyId, BetaReaderRoleId);

        Func<Task> act = () => AcceptAsync(storyId, BetaReaderRoleId);
        await act.Should().ThrowAsync<StoryAcknowledgmentValidationException>(
            "accepting twice must not double-count the counter or re-fire the badge check");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // DeclineAsync
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Decline_Recipient_FlipsToDeclinedNoCounterChange()
    {
        int storyId = await SeedStoryAsync(authorId: _authorId);
        SetActiveUser(_authorId);
        await RequestAsync(storyId, _recipientId, BetaReaderRoleId);

        SetActiveUser(_recipientId);
        await DeclineAsync(storyId, BetaReaderRoleId);

        StoryAcknowledgment? row = await GetRawAsync(storyId, _recipientId, BetaReaderRoleId);
        row!.StatusId.Should().Be(StoryAcknowledgmentStatus.Declined, "kept as a row, not deleted");
        (await LoadCounterAsync(_recipientId)).Should().Be(0, "a Pending credit was never counted");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // RevokeAsync — transition-delta decrement
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Revoke_WhileAccepted_RemovesRowAndDecrementsCounter()
    {
        await SeedUserStatAsync(_recipientId);
        int storyId = await SeedStoryAsync(authorId: _authorId);
        SetActiveUser(_authorId);
        await RequestAsync(storyId, _recipientId, BetaReaderRoleId);

        SetActiveUser(_recipientId);
        await AcceptAsync(storyId, BetaReaderRoleId);
        (await LoadCounterAsync(_recipientId)).Should().Be(1);

        SetActiveUser(_authorId);
        await RevokeAsync(storyId, _recipientId, BetaReaderRoleId);

        (await GetRawAsync(storyId, _recipientId, BetaReaderRoleId)).Should().BeNull("revoke deletes the row entirely");
        (await LoadCounterAsync(_recipientId)).Should().Be(0,
            "transition-delta: the credit WAS counted (Accepted), so removing it must undo that");
    }

    [Fact]
    public async Task Revoke_WhilePending_RemovesRowWithoutTouchingCounter()
    {
        int storyId = await SeedStoryAsync(authorId: _authorId);
        SetActiveUser(_authorId);
        await RequestAsync(storyId, _recipientId, BetaReaderRoleId);

        await RevokeAsync(storyId, _recipientId, BetaReaderRoleId);

        (await GetRawAsync(storyId, _recipientId, BetaReaderRoleId)).Should().BeNull();
        (await LoadCounterAsync(_recipientId)).Should().Be(0, "a Pending credit was never counted, so nothing to undo");
    }

    [Fact]
    public async Task Revoke_NotStoryOwner_ThrowsUnauthorizedAccess()
    {
        int storyId = await SeedStoryAsync(authorId: _authorId);
        SetActiveUser(_authorId);
        await RequestAsync(storyId, _recipientId, BetaReaderRoleId);

        SetActiveUser(_recipientId); // owns the credit as recipient, not the story
        Func<Task> act = () => RevokeAsync(storyId, _recipientId, BetaReaderRoleId);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Revoke_NonExistent_NoThrow()
    {
        int storyId = await SeedStoryAsync(authorId: _authorId);
        SetActiveUser(_authorId);
        Func<Task> act = () => RevokeAsync(storyId, _recipientId, BetaReaderRoleId);
        await act.Should().NotThrowAsync("idempotent — a no-op if the credit doesn't exist");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // GetAcknowledgmentsForStoryAsync — public display
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAcknowledgmentsForStory_ReturnsOnlyAccepted()
    {
        int storyId = await SeedStoryAsync(authorId: _authorId);
        int pendingRecipientId = await SeedUserAsync("pending-recipient");

        SetActiveUser(_authorId);
        await RequestAsync(storyId, _recipientId, BetaReaderRoleId);
        await RequestAsync(storyId, pendingRecipientId, PlannerRoleId);

        SetActiveUser(_recipientId);
        await AcceptAsync(storyId, BetaReaderRoleId);
        // pendingRecipientId's credit stays Pending — never accepted.

        IReadOnlyList<StoryAcknowledgmentDto> credits = await GetForStoryAsync(storyId);

        credits.Should().ContainSingle();
        credits[0].AcknowledgedUserId.Should().Be(_recipientId);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // GetManageDataForUserAsync
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetManageData_Incoming_OnlyPendingNamingCallerAsRecipient()
    {
        int storyId = await SeedStoryAsync(authorId: _authorId);
        int otherRecipientId = await SeedUserAsync("other-recipient");

        SetActiveUser(_authorId);
        await RequestAsync(storyId, _recipientId, BetaReaderRoleId);
        await RequestAsync(storyId, otherRecipientId, PlannerRoleId);

        SetActiveUser(_recipientId);
        StoryAcknowledgmentManageDto manage = await GetManageDataAsync();

        manage.IncomingRequests.Should().ContainSingle(r =>
            r.StoryId == storyId && r.RoleId == BetaReaderRoleId);
    }

    [Fact]
    public async Task GetManageData_Outgoing_IncludesAllStatuses()
    {
        int storyId = await SeedStoryAsync(authorId: _authorId);
        int declinedRecipientId = await SeedUserAsync("declined-recipient");

        SetActiveUser(_authorId);
        await RequestAsync(storyId, _recipientId, BetaReaderRoleId);
        await RequestAsync(storyId, declinedRecipientId, PlannerRoleId);

        SetActiveUser(_recipientId);
        await AcceptAsync(storyId, BetaReaderRoleId);
        SetActiveUser(declinedRecipientId);
        await DeclineAsync(storyId, PlannerRoleId);

        SetActiveUser(_authorId);
        StoryAcknowledgmentManageDto manage = await GetManageDataAsync();

        manage.Outgoing.Should().HaveCount(2);
        manage.Outgoing.Select(o => o.Status).Should().BeEquivalentTo(
            [StoryAcknowledgmentStatus.Accepted, StoryAcknowledgmentStatus.Declined]);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Private helpers — service calls
    // ─────────────────────────────────────────────────────────────────────────────

    private async Task RequestAsync(int storyId, int acknowledgedUserId, short roleId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryAcknowledgmentWriteService svc = scope.ServiceProvider.GetRequiredService<IStoryAcknowledgmentWriteService>();
        await svc.RequestAcknowledgmentAsync(new CreateStoryAcknowledgmentDto
        {
            StoryId = storyId,
            AcknowledgedUserId = acknowledgedUserId,
            AcknowledgmentRoleId = roleId
        });
    }

    private async Task AcceptAsync(int storyId, short roleId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryAcknowledgmentWriteService svc = scope.ServiceProvider.GetRequiredService<IStoryAcknowledgmentWriteService>();
        await svc.AcceptAsync(storyId, roleId);
    }

    private async Task DeclineAsync(int storyId, short roleId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryAcknowledgmentWriteService svc = scope.ServiceProvider.GetRequiredService<IStoryAcknowledgmentWriteService>();
        await svc.DeclineAsync(storyId, roleId);
    }

    private async Task RevokeAsync(int storyId, int acknowledgedUserId, short roleId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryAcknowledgmentWriteService svc = scope.ServiceProvider.GetRequiredService<IStoryAcknowledgmentWriteService>();
        await svc.RevokeAsync(storyId, acknowledgedUserId, roleId);
    }

    private async Task<IReadOnlyList<StoryAcknowledgmentDto>> GetForStoryAsync(int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryAcknowledgmentReadService svc = scope.ServiceProvider.GetRequiredService<IStoryAcknowledgmentReadService>();
        return await svc.GetAcknowledgmentsForStoryAsync(storyId);
    }

    private async Task<StoryAcknowledgmentManageDto> GetManageDataAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryAcknowledgmentReadService svc = scope.ServiceProvider.GetRequiredService<IStoryAcknowledgmentReadService>();
        return await svc.GetManageDataForUserAsync();
    }

    /// <summary>Raw ground-truth read, bypassing the service.</summary>
    private async Task<StoryAcknowledgment?> GetRawAsync(int storyId, int acknowledgedUserId, short roleId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.StoryAcknowledgments.AsNoTracking().FirstOrDefaultAsync(a =>
            a.StoryId == storyId && a.AcknowledgedUserId == acknowledgedUserId && a.AcknowledgmentRoleId == roleId);
    }

    private async Task<int> LoadCounterAsync(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.UserStats
            .Where(us => us.UserId == userId)
            .Select(us => us.AcknowledgedAsBetaReaderCount)
            .FirstOrDefaultAsync();
    }

    /// <summary>The same-transaction ExecuteUpdateAsync producer is a silent no-op when the
    /// target has no UserStat row (no production write path creates one at registration —
    /// UserStatRecalculator is the real first populator). Must be called before exercising any
    /// counter-touching path.</summary>
    private async Task SeedUserStatAsync(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.UserStats.Add(new UserStat { UserId = userId });
        await db.SaveChangesAsync();
    }

    private async Task<UserBadge?> LoadBadgeAsync(int userId, string badgeKey)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.UserBadges.AsNoTracking()
            .FirstOrDefaultAsync(ub => ub.UserId == userId && ub.BadgeKey == badgeKey);
    }
}
