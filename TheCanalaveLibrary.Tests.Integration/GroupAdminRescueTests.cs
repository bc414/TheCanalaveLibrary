using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// <see cref="IModerationWriteService.AssignGroupAdminAsync"/> — the moderator's remedy for an admin-less
/// group (owner ruling D13 edge (b), WU-UserDeletion 2026-10-13).
///
/// <para><b>What's tested:</b> both routes to an admin-less group (the founder deletes their account;
/// the last admin leaves); the happy path (the member becomes Admin; one opened-and-resolved
/// <c>Report</c> row with the Group target, reason "Other", the notes, reporter == moderator, and the
/// founder snapshot as <c>ReportedUserId</c> — NULL on the deletion route; type 62 to the promoted user,
/// null-sourced and anchored on the group; <c>GroupsJoined</c> unmoved — D24; <c>AdminCount</c> 0 → 1);
/// the type-62 row read back through <see cref="INotificationReadService"/> with the group's name and
/// link, an M-audience group included (the enricher's Group kind);
/// exactly three guards (a group that still has an admin, a non-member target, a non-moderator or
/// anonymous caller) and the input checks (blank and over-long notes, an unknown group), each writing
/// nothing; no fourth guard (a moderator may assign themselves, and is not notified of their own act);
/// <c>Group</c> stays out of report submission (D47's floor); the queue's "Show resolved" and the
/// founder's history label the ledger row with the group's name.</para>
///
/// <para><b>Seeding (testing.md "FK parents"):</b> users via <c>SeedUserAsync</c>; the group through
/// <c>CreateGroupAsync</c>/<c>JoinAsync</c> so the member rows exist, with a <c>UserStat</c> row seeded
/// first for the counter assertion. Reason 1 ("Other") and notification type 62 are migration-seeded.
/// <c>AssignGroupAdminAsync</c> is a moderator work-surface write, so it is not enrolled in
/// <c>ParentVisibilityContractTests</c>.</para>
///
/// Tier: <b>Integration</b> (real Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class GroupAdminRescueTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private const string Notes = "Members asked in the group's comments; Dawn volunteered.";

    private int _modId;
    private int _creatorId;
    private int _memberId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _modId = await SeedUserAsync("mod");
        _creatorId = await SeedUserAsync("creator");
        _memberId = await SeedUserAsync("member");
    }

    // ── The happy path, on both routes ───────────────────────────────────────────

    [Fact]
    public async Task Assign_AfterTheFounderDeletedTheirAccount_PromotesTheMember_FilesTheLedgerRow_NotifiesNullSourced()
    {
        await SeedUserStatAsync(_memberId);
        int groupId = await CreateGroupWithMemberAsync();
        using (IServiceScope scope = Factory.Services.CreateScope())
            (await scope.ServiceProvider.GetRequiredService<UserDeletionService>().DeleteUserAsync(_creatorId))
                .Should().BeTrue();
        (await GetDetailAsync(groupId)).AdminCount.Should().Be(0, "the founder's deletion left the group admin-less");
        int groupsJoinedBefore = await GroupsJoinedAsync(_memberId);

        await AssignAsModeratorAsync(groupId, _memberId, Notes);

        using IServiceScope check = Factory.Services.CreateScope();
        ApplicationDbContext db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.GroupMembers.Where(m => m.GroupId == groupId && m.UserId == _memberId).Select(m => m.Role).SingleAsync())
            .Should().Be(GroupRole.Admin);

        var report = await db.Reports
            .Where(r => r.ReportedEntityType == ReportedEntityType.Group && r.ReportedEntityId == groupId)
            .Select(r => new
            {
                r.ReportReasonId, r.Notes, r.ActionTaken, r.ReporterUserId, r.ModeratorUserId,
                r.ReportStatusId, r.DateReported, r.DateResolved, r.ReportedUserId,
            })
            .SingleAsync();
        report.ReportReasonId.Should().Be(1, "the seeded 'Other' — no misconduct reason fits an administrative assignment");
        report.Notes.Should().Be(Notes);
        report.ActionTaken.Should().Be(Notes);
        report.ReporterUserId.Should().Be(_modId);
        report.ModeratorUserId.Should().Be(_modId, "reporter == moderator marks the row moderator-initiated");
        report.ReportStatusId.Should().Be(ReportStatusEnum.ResolvedActionTaken);
        report.DateResolved.Should().Be(report.DateReported, "opened and resolved together");
        report.ReportedUserId.Should().BeNull(
            "the founder snapshot (D8's Group arm) — NULL once the founder deleted their account, by design");

        var notification = await db.Notifications
            .Where(n => n.NotificationTypeId == NotificationTypeEnum.GroupAdminAssigned)
            .Select(n => new { n.RecipientUserId, n.SourceUserId, n.RelatedEntityId })
            .SingleAsync();
        notification.RecipientUserId.Should().Be(_memberId);
        notification.SourceUserId.Should().BeNull(
            "the moderator is never named (a default under D5's threat model; roadmap decision row 23)");
        notification.RelatedEntityId.Should().Be(groupId);

        (await GroupsJoinedAsync(_memberId)).Should().Be(groupsJoinedBefore,
            "a role change on an existing row is not a membership change (D24)");
        (await GetDetailAsync(groupId)).AdminCount.Should().Be(1);
    }

    [Fact]
    public async Task Assign_AfterTheLastAdminLeft_RecordsTheFounderAsTheAnswerableAccount()
    {
        int groupId = await CreateGroupWithMemberAsync();
        await LeaveAsync(groupId, _creatorId);

        await AssignAsModeratorAsync(groupId, _memberId, Notes);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Reports.Where(r => r.ReportedEntityType == ReportedEntityType.Group && r.ReportedEntityId == groupId)
                .Select(r => r.ReportedUserId).SingleAsync())
            .Should().Be(_creatorId, "the founder is a snapshot, not the current admins (D8's seventh-type amendment)");
    }

    [Fact]
    public async Task TheNotification_ReadsBackWithTheGroupsNameAndLink_EvenForAnMAudienceGroup()
    {
        // D13: the promoted user "must be able to find out". Without NotificationEnricher's 62 → Group arm the
        // row reaches the bell, the inbox and the email with no title and no link (WU-UserDeletion review fixes).
        int groupId = await CreateGroupWithMemberAsync();
        await LeaveAsync(groupId, _creatorId);
        await AssignAsModeratorAsync(groupId, _memberId, Notes);
        string groupName;
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            groupName = await db.Groups.Where(g => g.GroupId == groupId).Select(g => g.GroupName).SingleAsync();
            // An M-audience group: the member reads with mature content off, so the Group kind's
            // GroupAudience bypass (Personal plane) is what keeps the name.
            await db.Groups.Where(g => g.GroupId == groupId)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.AudienceRating, Rating.M));
        }

        SetActiveUser(_memberId); // ShowMatureContent = false
        NotificationDto dto;
        using (IServiceScope scope = Factory.Services.CreateScope())
            dto = (await scope.ServiceProvider.GetRequiredService<INotificationReadService>().GetNotificationsAsync(1, 50))
                .Should().ContainSingle(n => n.NotificationTypeId == NotificationTypeEnum.GroupAdminAssigned).Subject;

        dto.TargetTitle.Should().Be(groupName);
        dto.TargetUrl.Should().Be($"/group/{groupId}");
        dto.SourceUserId.Should().BeNull();
    }

    // ── Exactly three guards ─────────────────────────────────────────────────────

    [Fact]
    public async Task Assign_GroupThatStillHasAnAdmin_IsRefused_AndWritesNothing()
    {
        int groupId = await CreateGroupWithMemberAsync(); // the founder is still its admin

        Func<Task> act = () => AssignAsModeratorAsync(groupId, _memberId, Notes);

        await act.Should().ThrowAsync<ModerationValidationException>().WithMessage("*still has an admin*");
        await AssertNothingWrittenAsync(groupId);
    }

    [Fact]
    public async Task Assign_TargetWhoIsNotAMember_IsRefused_AndWritesNothing()
    {
        int groupId = await CreateGroupWithMemberAsync();
        await LeaveAsync(groupId, _creatorId);
        int outsider = await SeedUserAsync("outsider");

        Func<Task> act = () => AssignAsModeratorAsync(groupId, outsider, Notes);

        await act.Should().ThrowAsync<ModerationValidationException>().WithMessage("*isn't a member*");
        await AssertNothingWrittenAsync(groupId);
    }

    [Fact]
    public async Task Assign_ByANonModerator_IsUnauthorized_AndByAnonymous_IsInvalidOperation()
    {
        int groupId = await CreateGroupWithMemberAsync();
        await LeaveAsync(groupId, _creatorId);

        SetActiveUser(_memberId); // signed in, no moderator role — even the target themselves
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            Func<Task> member = () => scope.ServiceProvider.GetRequiredService<IModerationWriteService>()
                .AssignGroupAdminAsync(groupId, _memberId, Notes);
            await member.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        SetActiveUser(FakeActiveUserContext.Anonymous());
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            Func<Task> anonymous = () => scope.ServiceProvider.GetRequiredService<IModerationWriteService>()
                .AssignGroupAdminAsync(groupId, _memberId, Notes);
            await anonymous.Should().ThrowAsync<InvalidOperationException>();
        }

        await AssertNothingWrittenAsync(groupId);
    }

    // ── Input checks ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Assign_WithBlankNotes_IsRefused(string notes)
    {
        int groupId = await CreateGroupWithMemberAsync();
        await LeaveAsync(groupId, _creatorId);

        Func<Task> act = () => AssignAsModeratorAsync(groupId, _memberId, notes);

        await act.Should().ThrowAsync<ModerationValidationException>().WithMessage("*Notes are required*");
        await AssertNothingWrittenAsync(groupId);
    }

    [Fact]
    public async Task Assign_WithNotesOverTheColumnCap_IsRefused()
    {
        int groupId = await CreateGroupWithMemberAsync();
        await LeaveAsync(groupId, _creatorId);

        Func<Task> act = () => AssignAsModeratorAsync(groupId, _memberId, new string('n', 1025));

        await act.Should().ThrowAsync<ModerationValidationException>().WithMessage("*too long*");
        await AssertNothingWrittenAsync(groupId);
    }

    [Fact]
    public async Task Assign_UnknownGroup_IsNotFound()
    {
        Func<Task> act = () => AssignAsModeratorAsync(groupId: int.MaxValue, _memberId, Notes);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── No fourth guard ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Assign_AModeratorWhoIsAMember_MayAssignThemselves_AndIsNotNotifiedOfTheirOwnAct()
    {
        int groupId = await CreateGroupWithMemberAsync();
        SetActiveUser(_modId);
        using (IServiceScope scope = Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IGroupWriteService>().JoinAsync(groupId);
        await LeaveAsync(groupId, _creatorId);

        await AssignAsModeratorAsync(groupId, _modId, Notes);

        using IServiceScope check = Factory.Services.CreateScope();
        ApplicationDbContext db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.GroupMembers.Where(m => m.GroupId == groupId && m.UserId == _modId).Select(m => m.Role).SingleAsync())
            .Should().Be(GroupRole.Admin, "D13 lists exactly three guards; a self-target is not one");
        (await db.Notifications.AnyAsync(n => n.NotificationTypeId == NotificationTypeEnum.GroupAdminAssigned))
            .Should().BeFalse("the acting moderator is never a recipient of a notification about their own act");
    }

    // ── Group stays ledger-only (D47's floor) ────────────────────────────────────

    [Fact]
    public async Task SubmitReport_AgainstAGroup_IsRefused_GroupIsLedgerOnly()
    {
        int groupId = await CreateGroupWithMemberAsync();
        SetActiveUser(_memberId);
        using IServiceScope scope = Factory.Services.CreateScope();

        Func<Task> act = () => scope.ServiceProvider.GetRequiredService<IReportSubmissionService>()
            .SubmitReportAsync(new SubmitReportRequest(ReportedEntityType.Group, groupId, 1, "inappropriate name"));

        // Amendment: ModerationIntegrity B1 made the allow-set refusal a ModerationValidationException (400).
        await act.Should().ThrowAsync<ModerationValidationException>().WithMessage("*cannot be reported*");
    }

    [Fact]
    public async Task TheLedgerRow_IsLabelledInShowResolved_AndInTheFoundersHistory_ButNotInTheOpenQueue()
    {
        int groupId = await CreateGroupWithMemberAsync();
        await LeaveAsync(groupId, _creatorId);
        await AssignAsModeratorAsync(groupId, _memberId, Notes);
        string groupName = (await GetDetailAsync(groupId)).GroupName;

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using IServiceScope scope = Factory.Services.CreateScope();
        IModerationReadService reads = scope.ServiceProvider.GetRequiredService<IModerationReadService>();

        ReportQueueItemDto row = (await reads.GetReportQueueAsync(includeResolved: true))
            .Should().ContainSingle(r => r.EntityType == ReportedEntityType.Group).Subject;
        row.EntityId.Should().Be(groupId);
        row.TargetLabel.Should().Be($"Group: {groupName}", "without the Group arm the queue would drop the row");
        row.TargetUrl.Should().Be($"/group/{groupId}");
        row.Status.Should().Be(ReportStatusEnum.ResolvedActionTaken, "born resolved — D47(a)'s floor: no action offered");

        (await reads.GetReportQueueAsync(includeResolved: false))
            .Should().NotContain(r => r.EntityType == ReportedEntityType.Group);

        UserModerationHistoryDto history = (await reads.GetUserModerationHistoryAsync(_creatorId))!;
        history.Reports.Should().ContainSingle(r => r.EntityType == ReportedEntityType.Group)
            .Which.TargetLabel.Should().Be($"Group: {groupName}");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>The founder creates a group (and becomes its admin); <see cref="_memberId"/> joins.</summary>
    private async Task<int> CreateGroupWithMemberAsync()
    {
        int groupId;
        SetActiveUser(_creatorId);
        using (IServiceScope scope = Factory.Services.CreateScope())
            groupId = await scope.ServiceProvider.GetRequiredService<IGroupWriteService>()
                .CreateGroupAsync(new CreateGroupDto { GroupName = $"Rescue {Guid.NewGuid():N}" });
        SetActiveUser(_memberId);
        using (IServiceScope scope = Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IGroupWriteService>().JoinAsync(groupId);
        return groupId;
    }

    private async Task LeaveAsync(int groupId, int userId)
    {
        SetActiveUser(userId);
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IGroupWriteService>().LeaveAsync(groupId);
    }

    private async Task AssignAsModeratorAsync(int groupId, int targetUserId, string notes)
    {
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IModerationWriteService>()
            .AssignGroupAdminAsync(groupId, targetUserId, notes);
    }

    private async Task<GroupDetailDto> GetDetailAsync(int groupId)
    {
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using IServiceScope scope = Factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<IGroupReadService>().GetByIdAsync(groupId))!;
    }

    private async Task SeedUserStatAsync(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.UserStats.Add(new UserStat { UserId = userId });
        await db.SaveChangesAsync();
    }

    private async Task<int> GroupsJoinedAsync(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().UserStats
            .Where(s => s.UserId == userId).Select(s => s.GroupsJoined).SingleAsync();
    }

    private async Task AssertNothingWrittenAsync(int groupId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Reports.AnyAsync(r => r.ReportedEntityType == ReportedEntityType.Group)).Should().BeFalse();
        (await db.Notifications.AnyAsync(n => n.NotificationTypeId == NotificationTypeEnum.GroupAdminAssigned))
            .Should().BeFalse();
        (await db.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == _memberId && m.Role == GroupRole.Admin))
            .Should().BeFalse();
    }
}
