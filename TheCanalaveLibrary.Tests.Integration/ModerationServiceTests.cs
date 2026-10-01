using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="IModerationWriteService"/> and the supporting notification
/// flow (WU34 — Features 46/47/48).
///
/// <para><b>What's tested:</b>
/// <list type="bullet">
///   <item><c>SubmitReportAsync</c> (now on <see cref="IReportSubmissionService"/>, owner ruling D9):
///   creates Report row, increments <c>ActiveReportCount</c>.</item>
///   <item>Invalid target type throws immediately (allow-set gate) — a
///   <c>ModerationValidationException</c> since WU-ModerationIntegrity.</item>
///   <item>The report-lifecycle integrity rules WU-ModerationIntegrity built (D7 sibling closing,
///   status guards, dedup, <c>ReportedUserId</c>, the account-status table, the read gates) are in
///   <see cref="ModerationIntegrityTests"/>.</item>
///   <item><c>ResolveNoActionAsync</c>: status → ResolvedNoAction, count decremented, notification sent.</item>
///   <item><c>ResolveWithRemovalAsync</c> (soft takedown): sets <c>IsTakenDown=true</c>, drops from
///   public reads, remains visible with <c>IgnoreQueryFilters(["IsTakenDown"])</c>.</item>
///   <item>The moderation notification band (owner rulings D4/D5, WU-InertFeatures): every row in
///   70–82 is null-sourced (no moderator id ever reaches the recipient); <c>ReportReceived</c> is
///   delivered again, carrying the report id; 70/80/81/82 carry report ids so two outcomes never
///   collapse; account actions (72–74) are exempt from cross-existing dedup; the moderator-initiated
///   account action sends no report receipts (the D4 guardrail); story-outcome dedup still collapses
///   an identical unread pair across null sources.</item>
///   <item><c>ApproveStoryAsync</c>: sets <c>StoryStatusId = PostApprovalStatus</c>, stamps
///   <c>PublishedDate</c> on first publication, adds 1 to the author's monotonic
///   <c>ApprovedStorySubmissions</c>, fires <c>StoryApproved</c> — and (WU-StoryLifecycle, D1)
///   refuses with <c>ModerationValidationException</c> when the row is no longer pending (double
///   approve, author withdraw), its <c>PostApprovalStatus</c> is not an entry status, or its author is
///   deleted/banned/suspended; an unknown story is <c>KeyNotFoundException</c>.</item>
///   <item><c>RejectStoryAsync</c>: sets <c>StoryStatusId = Rejected</c>, records reason, fires
///   <c>StoryRejected</c>; only from <c>PendingApproval</c>, never guarded on the author.</item>
///   <item><c>SetCanAutoApproveAsync</c> (WU-StoryLifecycle): flips the flag in both directions and
///   files a moderator-initiated audit <c>Report</c> each time; unchanged value writes nothing; unknown
///   user/reason and an over-long reason are refused.</item>
///   <item>The pending queue orders by and returns <c>SubmittedDate</c>, and never lists a taken-down
///   story, which approve and reject both refuse (a taken-down story's status is frozen).</item>
///   <item>The conditional writes themselves (review fixes, 2026-09-30): a competing write landing
///   between the pre-read and the update (<see cref="InterleavingCommandInterceptor"/>) is refused
///   with nothing written, and a failed trust-record write rolls the approve's status flip back.</item>
/// </list>
/// </para>
///
/// Tier: <b>Integration</b> (real Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class ModerationServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    // ── Shared state for each test (set in InitializeAsync) ──────────────────────

    private int _reporterId;
    private int _modId;
    private readonly List<IServiceScope> _serviceScopes = [];

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _reporterId = await SeedUserAsync("Reporter");
        _modId = await SeedUserAsync("Moderator");
    }

    // ── SubmitReportAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitReportAsync_CreatesReportRow_IncrementsStoryActiveReportCount()
    {
        int authorId = await SeedUserAsync("Author");
        int storyId = await SeedStoryAsync(authorId);
        short reasonId = await GetFirstReasonIdAsync();

        SetActiveUser(_reporterId);
        await GetSubmission().SubmitReportAsync(new SubmitReportRequest(
            ReportedEntityType.Story, storyId, reasonId, "test notes"));

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Report? report = await db.Reports
            .FirstOrDefaultAsync(r => r.ReportedEntityType == ReportedEntityType.Story
                                   && r.ReportedEntityId == storyId);
        report.Should().NotBeNull();
        report!.ReporterUserId.Should().Be(_reporterId);
        report.ReportStatusId.Should().Be(ReportStatusEnum.Open);
        report.Notes.Should().Be("test notes");

        Story story = await db.Stories.IgnoreQueryFilters(["IsTakenDown"])
            .SingleAsync(s => s.StoryId == storyId);
        story.ActiveReportCount.Should().Be(1);
    }

    [Fact]
    public async Task SubmitReportAsync_InvalidTargetType_Throws()
    {
        SetActiveUser(_reporterId);
        short reasonId = await GetFirstReasonIdAsync();

        Func<Task> act = () => GetSubmission().SubmitReportAsync(new SubmitReportRequest(
            (ReportedEntityType)99, 1, reasonId, null));

        // A business rule, so a 400 (ModerationValidationException) — it was an
        // InvalidOperationException, which the endpoint layer maps to 401 (WU-ModerationIntegrity).
        await act.Should().ThrowAsync<ModerationValidationException>()
            .WithMessage("*cannot be reported*");
    }

    // ── ClaimReportAsync / ResolveNoActionAsync ────────────────────────────────────

    [Fact]
    public async Task ResolveNoActionAsync_DecrementsCount_SetsStatus_NotifiesReporter()
    {
        int storyId = await SeedStoryAsync();
        long reportId = await SeedReportAsync(ReportedEntityType.Story, storyId, _reporterId);

        // Claim then resolve no action as moderator.
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await GetMod().ClaimReportAsync(reportId);
        await GetMod().ResolveNoActionAsync(reportId, "looks fine");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Report report = await db.Reports.SingleAsync(r => r.ReportId == reportId);
        report.ReportStatusId.Should().Be(ReportStatusEnum.ResolvedNoAction);
        report.ModeratorUserId.Should().Be(_modId);
        report.ActionTaken.Should().Be("looks fine");
        report.DateResolved.Should().NotBeNull();

        Story story = await db.Stories.IgnoreQueryFilters(["IsTakenDown"])
            .SingleAsync(s => s.StoryId == storyId);
        story.ActiveReportCount.Should().Be(0);

        // ReportResolvedNoAction notification should exist for the reporter.
        bool hasNotif = await db.Notifications.AnyAsync(n =>
            n.RecipientUserId == _reporterId &&
            n.NotificationTypeId == NotificationTypeEnum.ReportResolvedNoAction);
        hasNotif.Should().BeTrue();
    }

    // ── ResolveWithRemovalAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveWithRemovalAsync_SoftHides_DropsFromPublicQuery_VisibleWithIgnoreFilter()
    {
        int authorId = await SeedUserAsync("ContentAuthor");
        int storyId = await SeedStoryAsync(authorId, Rating.E);
        long reportId = await SeedReportAsync(ReportedEntityType.Story, storyId, _reporterId);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await GetMod().ClaimReportAsync(reportId);
        await GetMod().ResolveWithRemovalAsync(reportId, "rule violation");

        using IServiceScope scope = Factory.Services.CreateScope();
        // Use write context (unfiltered) to assert ground-truth state.
        ApplicationDbContext writeDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // Use read context (filtered) to assert the public-visibility invariant.
        ReadOnlyApplicationDbContext readDb = scope.ServiceProvider.GetRequiredService<ReadOnlyApplicationDbContext>();

        Story story = await writeDb.Stories
            .SingleAsync(s => s.StoryId == storyId);
        story.IsTakenDown.Should().BeTrue();
        story.TakedownReason.Should().Be("rule violation");
        story.TakedownDate.Should().NotBeNull();

        // Public query via read context (IsTakenDown filter active) should not find the story.
        bool visiblePublicly = await readDb.Stories
            .AnyAsync(s => s.StoryId == storyId);
        visiblePublicly.Should().BeFalse();

        // ContentRemoved notification should go to the author.
        bool authorNotified = await writeDb.Notifications.AnyAsync(n =>
            n.RecipientUserId == authorId &&
            n.NotificationTypeId == NotificationTypeEnum.ContentRemoved);
        authorNotified.Should().BeTrue();
    }

    // ── Story-outcome dedup (RelatedEntityId in the key, null source on both sides) ─

    [Fact]
    public async Task NotifyStoryRejectedAsync_TwoDifferentStories_BothNotificationsLand()
    {
        int storyA = await SeedStoryAsync();
        int storyB = await SeedStoryAsync();

        using IServiceScope scope = Factory.Services.CreateScope();
        INotificationWriteService notifSvc =
            scope.ServiceProvider.GetRequiredService<INotificationWriteService>();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Same type, same (null) source, different RelatedEntityIds → two rows.
        await notifSvc.NotifyStoryRejectedAsync(_reporterId, storyA);
        await notifSvc.NotifyStoryRejectedAsync(_reporterId, storyB);

        List<Notification> rows = await db.Notifications.Where(n =>
            n.RecipientUserId == _reporterId &&
            n.NotificationTypeId == NotificationTypeEnum.StoryRejected).ToListAsync();
        rows.Should().HaveCount(2, "each distinct RelatedEntityId should produce its own notification");
        rows.Should().OnlyContain(n => n.SourceUserId == null, "D5: the moderation band is null-sourced");
    }

    [Fact]
    public async Task NotifyStoryApprovedAsync_SameStoryTwiceWhileUnread_SecondDeduped()
    {
        int storyId = await SeedStoryAsync();

        using IServiceScope scope = Factory.Services.CreateScope();
        INotificationWriteService notifSvc =
            scope.ServiceProvider.GetRequiredService<INotificationWriteService>();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await notifSvc.NotifyStoryApprovedAsync(_reporterId, storyId);
        await notifSvc.NotifyStoryApprovedAsync(_reporterId, storyId);

        int count = await db.Notifications.CountAsync(n =>
            n.RecipientUserId == _reporterId &&
            n.NotificationTypeId == NotificationTypeEnum.StoryApproved);
        count.Should().Be(1,
            "a null source matches a null source (IS NULL), so an identical unread pair still dedups");
    }

    // ── The moderation notification band (owner rulings D4/D5, WU-InertFeatures) ──

    [Fact]
    public async Task SubmitReportAsync_DeliversANullSourcedReceipt_CarryingTheReportId()
    {
        int storyId = await SeedStoryAsync();
        short reasonId = await GetFirstReasonIdAsync();

        SetActiveUser(_reporterId);
        await GetSubmission().SubmitReportAsync(new SubmitReportRequest(
            ReportedEntityType.Story, storyId, reasonId, null));

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        long reportId = await db.Reports.Where(r => r.ReportedEntityId == storyId)
            .Select(r => r.ReportId).SingleAsync();

        Notification receipt = await db.Notifications.SingleAsync(n =>
            n.RecipientUserId == _reporterId && n.NotificationTypeId == NotificationTypeEnum.ReportReceived);
        receipt.SourceUserId.Should().BeNull(
            "D4: the receipt has no actor — passing the reporter as their own source is what made " +
            "drop-self delete every receipt before this WU");
        receipt.RelatedEntityId.Should().Be(reportId);
    }

    [Fact]
    public async Task SubmitReportAsync_TwoReports_TwoReceipts()
    {
        int storyA = await SeedStoryAsync();
        int storyB = await SeedStoryAsync();
        short reasonId = await GetFirstReasonIdAsync();

        SetActiveUser(_reporterId);
        await GetSubmission().SubmitReportAsync(new SubmitReportRequest(ReportedEntityType.Story, storyA, reasonId, null));
        await GetSubmission().SubmitReportAsync(new SubmitReportRequest(ReportedEntityType.Story, storyB, reasonId, null));

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        int receipts = await db.Notifications.CountAsync(n =>
            n.RecipientUserId == _reporterId && n.NotificationTypeId == NotificationTypeEnum.ReportReceived);
        receipts.Should().Be(2, "each receipt carries its own report id, so the unread first does not absorb the second");
    }

    [Fact]
    public async Task ResolvePaths_CarryTheReportId_AndNoModeratorSource()
    {
        int authorId = await SeedUserAsync("RemovedAuthor");
        int keptStory = await SeedStoryAsync(authorId);
        int removedStory = await SeedStoryAsync(authorId);
        long noActionReport = await SeedReportAsync(ReportedEntityType.Story, keptStory, _reporterId);
        long removalReport = await SeedReportAsync(ReportedEntityType.Story, removedStory, _reporterId);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await GetMod().ResolveNoActionAsync(noActionReport, "fine");
        await GetMod().ResolveWithRemovalAsync(removalReport, "rule violation");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Notification noAction = await db.Notifications.SingleAsync(n =>
            n.RecipientUserId == _reporterId && n.NotificationTypeId == NotificationTypeEnum.ReportResolvedNoAction);
        noAction.RelatedEntityId.Should().Be(noActionReport);
        noAction.SourceUserId.Should().BeNull();

        Notification resolved = await db.Notifications.SingleAsync(n =>
            n.RecipientUserId == _reporterId && n.NotificationTypeId == NotificationTypeEnum.ReportResolved);
        resolved.RelatedEntityId.Should().Be(removalReport);
        resolved.SourceUserId.Should().BeNull();

        Notification removed = await db.Notifications.SingleAsync(n =>
            n.RecipientUserId == authorId && n.NotificationTypeId == NotificationTypeEnum.ContentRemoved);
        removed.RelatedEntityId.Should().Be(removalReport,
            "70 anchors on the report row — a reported entity's id would collide across kinds");
        removed.SourceUserId.Should().BeNull();
    }

    [Fact]
    public async Task TwoRemovalsOfOneAuthorsItems_WhileTheFirstIsUnread_DeliverTwoContentRemoved()
    {
        int authorId = await SeedUserAsync("TwiceRemoved");
        int storyA = await SeedStoryAsync(authorId);
        int storyB = await SeedStoryAsync(authorId);
        long reportA = await SeedReportAsync(ReportedEntityType.Story, storyA, _reporterId);
        long reportB = await SeedReportAsync(ReportedEntityType.Story, storyB, _reporterId);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await GetMod().ResolveWithRemovalAsync(reportA, "violation A");
        await GetMod().ResolveWithRemovalAsync(reportB, "violation B");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        int removedRows = await db.Notifications.CountAsync(n =>
            n.RecipientUserId == authorId && n.NotificationTypeId == NotificationTypeEnum.ContentRemoved);
        removedRows.Should().Be(2,
            "spec §5.21 'reporters always learn the outcome' — with RelatedEntityId 0 the second removal " +
            "collapsed into the first while it was unread");
        int resolvedRows = await db.Notifications.CountAsync(n =>
            n.RecipientUserId == _reporterId && n.NotificationTypeId == NotificationTypeEnum.ReportResolved);
        resolvedRows.Should().Be(2);
    }

    [Fact]
    public async Task WarnTwiceWhileTheFirstIsUnread_DeliversTwoAccountWarnings()
    {
        int targetId = await SeedUserAsync("TwiceWarned");
        short reasonId = await GetFirstReasonIdAsync();

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await GetMod().ApplyAccountActionToUserAsync(targetId, reasonId, ModeratorActionType.WarnUser, "First.");
        await GetMod().ApplyAccountActionToUserAsync(targetId, reasonId, ModeratorActionType.WarnUser, "Second.");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        List<Notification> warnings = await db.Notifications.Where(n =>
            n.RecipientUserId == targetId && n.NotificationTypeId == NotificationTypeEnum.AccountWarning).ToListAsync();
        warnings.Should().HaveCount(2,
            "D4: account actions have no related entity (0), so they are exempt from cross-existing " +
            "dedup — two warnings are two rows");
        warnings.Should().OnlyContain(n => n.SourceUserId == null && n.RelatedEntityId == 0);
    }

    [Fact]
    public async Task ApplyAccountActionToUserAsync_SendsTheModeratorNoReportReceipts()
    {
        int targetId = await SeedUserAsync("GuardrailTarget");
        short reasonId = await GetFirstReasonIdAsync();

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await GetMod().ApplyAccountActionToUserAsync(targetId, reasonId, ModeratorActionType.WarnUser, "Mod-filed.");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        bool modGotReceipt = await db.Notifications.AnyAsync(n => n.RecipientUserId == _modId &&
            (n.NotificationTypeId == NotificationTypeEnum.ReportReceived
             || n.NotificationTypeId == NotificationTypeEnum.ReportResolved));
        modGotReceipt.Should().BeFalse(
            "D4 guardrail: the mod-filed report has ReporterUserId == ModeratorUserId, and a null " +
            "source no longer drop-selfs — wiring 80/81 here would mail the moderator about their own action");
        (await db.Notifications.AnyAsync(n => n.RecipientUserId == targetId
            && n.NotificationTypeId == NotificationTypeEnum.AccountWarning)).Should().BeTrue();
    }

    [Fact]
    public async Task ResolveWithRemoval_OnARecommendationReport_SweepsItsAttributions()
    {
        // FK parents: story author + story, the recommender + rec (+ detail), and a reader whose USI
        // row is the attribution's composite-FK parent (testing.md "FK parents").
        int authorId = await SeedUserAsync("RecStoryAuthor");
        int storyId = await SeedStoryAsync(authorId);
        int recommenderId = await SeedUserAsync("RemovedRecommender");
        int readerId = await SeedUserAsync("AttributedReader");
        int recId;
        using (IServiceScope seed = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Recommendation rec = new()
            {
                StoryId = storyId, RecommenderId = recommenderId,
                StatusId = (short)RecommendationStatusEnum.Approved, DatePosted = DateTime.UtcNow,
                RecommendationDetail = new RecommendationDetail { Text = "<p>endorsement</p>" },
            };
            db.Recommendations.Add(rec);
            await db.SaveChangesAsync();
            recId = rec.RecommendationId;
            db.UserStoryInteractions.Add(new UserStoryInteraction { UserId = readerId, StoryId = storyId, IsReadItLater = true });
            db.UserStoryRecommendationSources.Add(new UserStoryRecommendationSource
            {
                UserId = readerId, StoryId = storyId, SourceRecommendationId = recId,
            });
            await db.SaveChangesAsync();
        }
        long reportId = await SeedReportAsync(ReportedEntityType.Recommendation, recId, _reporterId);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await GetMod().ResolveWithRemovalAsync(reportId, "spam recommendation");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext verify = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await verify.UserStoryRecommendationSources.AnyAsync(s => s.SourceRecommendationId == recId))
            .Should().BeFalse("owner ruling D3, trigger 5: a taken-down rec can't be reminded or collect credit");
        (await verify.UserStoryInteractions.AnyAsync(i => i.UserId == readerId && i.IsReadItLater))
            .Should().BeTrue("the sweep removes the attribution, never the reader's Read It Later");
    }

    [Fact]
    public async Task NoNotificationInTheModerationBand_EverCarriesTheModeratorAsSource()
    {
        // Drive every moderator-facing path that notifies, then sweep the band.
        int authorId = await SeedUserAsync("BandAuthor");
        int reportedStory = await SeedStoryAsync(authorId);
        int removedStory = await SeedStoryAsync(authorId);
        long reportA = await SeedReportAsync(ReportedEntityType.Story, reportedStory, _reporterId);
        long reportB = await SeedReportAsync(ReportedEntityType.Story, removedStory, _reporterId);
        // A second reporter: one reporter holds at most one open report per target (the partial unique
        // index, service §2.4.4(c)).
        long reportC = await SeedReportAsync(ReportedEntityType.Story, reportedStory, await SeedUserAsync("SecondReporter"));
        int approveMe = await SeedPendingStoryAsync(authorId, StoryStatusEnum.InProgress);
        int rejectMe = await SeedPendingStoryAsync(authorId, StoryStatusEnum.InProgress);
        short reasonId = await GetFirstReasonIdAsync();

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await GetMod().ResolveNoActionAsync(reportA, "fine");
        await GetMod().ResolveWithRemovalAsync(reportB, "removed");
        await GetMod().ApplyAccountActionAsync(reportC, ModeratorActionType.WarnUser, "warned");
        await GetMod().ApproveStoryAsync(approveMe);
        await GetMod().RejectStoryAsync(rejectMe, "not yet");
        await GetMod().ApplyAccountActionToUserAsync(authorId, reasonId, ModeratorActionType.SuspendUser,
            "suspended", DateTime.UtcNow.AddDays(3));

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        List<Notification> band = await db.Notifications
            .Where(n => (short)n.NotificationTypeId >= 70 && (short)n.NotificationTypeId <= 82)
            .ToListAsync();

        band.Select(n => n.NotificationTypeId).Should().Contain(
        [
            NotificationTypeEnum.ReportResolvedNoAction, NotificationTypeEnum.ReportResolved,
            NotificationTypeEnum.ContentRemoved, NotificationTypeEnum.AccountWarning,
            NotificationTypeEnum.StoryApproved, NotificationTypeEnum.StoryRejected,
            NotificationTypeEnum.AccountSuspended,
        ]);
        band.Should().OnlyContain(n => n.SourceUserId == null,
            "D5: the acting moderator is never disclosed to the recipient — the id would ship in " +
            "NotificationDto over a WASM-reachable endpoint");
    }

    // ── The acting moderator is never a recipient (the D4 guardrail's general rule, review fixes) ──
    // Before D5 every band call passed the moderator as source, so drop-self kept their own acts out of
    // their bell. A null source drops nobody, so each call site must skip them explicitly.

    [Fact]
    public async Task AModeratorResolvingAReportTheyFiled_GetsNoResolutionReceipt()
    {
        int authorId = await SeedUserAsync("ReportedAuthor");
        int keptStory = await SeedStoryAsync(authorId);
        int removedStory = await SeedStoryAsync(authorId);
        short reasonId = await GetFirstReasonIdAsync();

        // The moderator files both through the ordinary Report button, then resolves them.
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await GetSubmission().SubmitReportAsync(new SubmitReportRequest(ReportedEntityType.Story, keptStory, reasonId, null));
        await GetSubmission().SubmitReportAsync(new SubmitReportRequest(ReportedEntityType.Story, removedStory, reasonId, null));
        long noActionReport, removalReport;
        using (IServiceScope lookup = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = lookup.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            noActionReport = await db.Reports.Where(r => r.ReportedEntityId == keptStory).Select(r => r.ReportId).SingleAsync();
            removalReport = await db.Reports.Where(r => r.ReportedEntityId == removedStory).Select(r => r.ReportId).SingleAsync();
        }
        await GetMod().ResolveNoActionAsync(noActionReport, "fine");
        await GetMod().ResolveWithRemovalAsync(removalReport, "rule violation");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext verify = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        List<NotificationTypeEnum> modTypes = await verify.Notifications
            .Where(n => n.RecipientUserId == _modId).Select(n => n.NotificationTypeId).ToListAsync();
        modTypes.Should().NotContain([NotificationTypeEnum.ReportResolved, NotificationTypeEnum.ReportResolvedNoAction],
            "the moderator resolved these reports themselves — a receipt would mail them their own act");
        modTypes.Count(t => t == NotificationTypeEnum.ReportReceived).Should().Be(2,
            "filing is the member act D4 restored the receipt for; only the resolution is the moderator's own");
        (await verify.Notifications.AnyAsync(n => n.RecipientUserId == authorId
            && n.NotificationTypeId == NotificationTypeEnum.ContentRemoved)).Should().BeTrue(
            "everyone else still hears the outcome");
    }

    [Fact]
    public async Task AModeratorRemovingTheirOwnContent_GetsNoContentRemoved_ButTheReporterHearsTheOutcome()
    {
        int modStory = await SeedStoryAsync(_modId);
        long reportId = await SeedReportAsync(ReportedEntityType.Story, modStory, _reporterId);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await GetMod().ResolveWithRemovalAsync(reportId, "removing my own story");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Notifications.AnyAsync(n => n.RecipientUserId == _modId
            && n.NotificationTypeId == NotificationTypeEnum.ContentRemoved)).Should().BeFalse();
        (await db.Notifications.AnyAsync(n => n.RecipientUserId == _reporterId
            && n.NotificationTypeId == NotificationTypeEnum.ReportResolved)).Should().BeTrue();
    }

    [Fact]
    public async Task AModeratorApprovingOrRejectingTheirOwnStory_GetsNoOutcomeNotification()
    {
        int approveMe = await SeedPendingStoryAsync(_modId, StoryStatusEnum.InProgress);
        int rejectMe = await SeedPendingStoryAsync(_modId, StoryStatusEnum.InProgress);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await GetMod().ApproveStoryAsync(approveMe);
        await GetMod().RejectStoryAsync(rejectMe, "not yet");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Notifications.AnyAsync(n => n.RecipientUserId == _modId
            && (n.NotificationTypeId == NotificationTypeEnum.StoryApproved
                || n.NotificationTypeId == NotificationTypeEnum.StoryRejected))).Should().BeFalse();
        (await db.Stories.IgnoreQueryFilters(["IsTakenDown"]).SingleAsync(s => s.StoryId == rejectMe))
            .StoryStatusId.Should().Be(StoryStatusEnum.Rejected, "the decision itself still lands");
    }

    [Fact]
    public async Task AReportDrivenAccountActionOnTheActingModerator_SendsThemNothing()
    {
        // A report against the moderator's own story resolves to the moderator as the action target.
        int modStory = await SeedStoryAsync(_modId);
        long reportId = await SeedReportAsync(ReportedEntityType.Story, modStory, _reporterId);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await GetMod().ApplyAccountActionAsync(reportId, ModeratorActionType.WarnUser, "self-warned");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Notifications.AnyAsync(n => n.RecipientUserId == _modId
            && n.NotificationTypeId == NotificationTypeEnum.AccountWarning)).Should().BeFalse();
        (await db.Reports.SingleAsync(r => r.ReportId == reportId)).ReportStatusId
            .Should().Be(ReportStatusEnum.ResolvedActionTaken, "the action itself still lands");
    }

    // ── ApproveStoryAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ApproveStoryAsync_SetsPostApprovalStatus_NotifiesAuthor()
    {
        int authorId = await SeedUserAsync("SubAuthor");
        int storyId = await SeedPendingStoryAsync(authorId, StoryStatusEnum.InProgress);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await GetMod().ApproveStoryAsync(storyId);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Story story = await db.Stories.IgnoreQueryFilters(["IsTakenDown"])
            .SingleAsync(s => s.StoryId == storyId);
        story.StoryStatusId.Should().Be(StoryStatusEnum.InProgress,
            "approve should set StoryStatusId to the PostApprovalStatus that was configured at submission");

        bool notified = await db.Notifications.AnyAsync(n =>
            n.RecipientUserId == authorId &&
            n.NotificationTypeId == NotificationTypeEnum.StoryApproved);
        notified.Should().BeTrue();
    }

    // ── RejectStoryAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RejectStoryAsync_SetsRejected_RecordsReason_NotifiesAuthor()
    {
        int authorId = await SeedUserAsync("SubAuthorReject");
        int storyId = await SeedPendingStoryAsync(authorId, StoryStatusEnum.InProgress);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await GetMod().RejectStoryAsync(storyId, "needs more description");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Story story = await db.Stories.IgnoreQueryFilters(["IsTakenDown"])
            .SingleAsync(s => s.StoryId == storyId);
        story.StoryStatusId.Should().Be(StoryStatusEnum.Rejected);
        story.TakedownReason.Should().Be("needs more description");

        bool notified = await db.Notifications.AnyAsync(n =>
            n.RecipientUserId == authorId &&
            n.NotificationTypeId == NotificationTypeEnum.StoryRejected);
        notified.Should().BeTrue();
    }

    // ── Report queue / pending submissions are work surfaces (decision row 1, 2026-07-18) ────

    [Fact]
    public async Task GetReportQueueAsync_ShowsMRatedStoryReport_ToModWithMatureOff()
    {
        int authorId = await SeedUserAsync("MRatedAuthor");
        int storyId = await SeedStoryAsync(authorId, Rating.M);
        long reportId = await SeedReportAsync(ReportedEntityType.Story, storyId, _reporterId);

        // FakeActiveUserContext.Moderator defaults ShowMatureContent = false.
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        ReportQueueItemDto[] queue = await GetMod().GetReportQueueAsync();

        queue.Should().Contain(r => r.ReportId == reportId,
            "the report queue is a work surface exempt from the moderator's personal " +
            "ShowMatureContent setting — an M-rated story's report must still surface");
    }

    [Fact]
    public async Task GetPendingSubmissionsAsync_ShowsMRatedSubmission_ToModWithMatureOff()
    {
        int authorId = await SeedUserAsync("MRatedSubAuthor");
        int storyId = await SeedPendingStoryAsync(authorId, StoryStatusEnum.InProgress, Rating.M);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        StorySubmissionQueueItemDto[] pending = await GetMod().GetPendingSubmissionsAsync();

        pending.Should().Contain(s => s.StoryId == storyId,
            "pending submissions are a work surface exempt from the moderator's personal " +
            "ShowMatureContent setting — an M-rated submission must still surface for approval");
    }

    // ── Non-moderator ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ClaimReportAsync_AsNonModerator_Throws()
    {
        int storyId = await SeedStoryAsync();
        long reportId = await SeedReportAsync(ReportedEntityType.Story, storyId, _reporterId);

        SetActiveUser(_reporterId); // plain user, not moderator
        Func<Task> act = () => GetMod().ClaimReportAsync(reportId);

        // Authenticated-but-not-a-mod is forbidden (403), not unauthenticated (401) — matches
        // Spotlight/SiteSettings/Poll's identical role gates (MA-123/MA-701).
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Moderator*");
    }

    // ── Account actions (WU-UserModeration) ───────────────────────────────────────

    [Fact]
    public async Task ApplyAccountActionAsync_OnStoryReport_ActsOnTheStorysAuthor()
    {
        int authorId = await SeedUserAsync("StoryAuthor");
        int storyId = await SeedStoryAsync(authorId);
        long reportId = await SeedReportAsync(ReportedEntityType.Story, storyId, _reporterId);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await GetMod().ApplyAccountActionAsync(reportId, ModeratorActionType.WarnUser, "Off-topic content.");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        User author = await db.Users.SingleAsync(u => u.Id == authorId);
        author.AccountStatus.Should().Be(AccountStatusEnum.Warned,
            "warning over a reported story means warning the person who wrote it — before " +
            "WU-UserModeration this call threw for every non-User report, i.e. for every report " +
            "the app can actually produce");

        Report report = await db.Reports.SingleAsync(r => r.ReportId == reportId);
        report.ReportStatusId.Should().Be(ReportStatusEnum.ResolvedActionTaken);
        report.ActionTaken.Should().Be("Off-topic content.");
    }

    [Fact]
    public async Task ApplyAccountActionAsync_OnCommentReport_ActsOnTheCommentsAuthor()
    {
        int commenterId = await SeedUserAsync("Commenter");

        // FK parents (testing.md "FK parents"): a UserProfileComment needs only two existing users
        // — the profile owner and the comment's author. Deliberately not a ChapterComment:
        // SeedStoryAsync creates no chapters, so that shape would need a chapter seeded too.
        long commentId;
        using (IServiceScope seedScope = Factory.Services.CreateScope())
        {
            ApplicationDbContext seedDb = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // BaseComment.UserId is the author FK; IModeratableContent.AuthorUserId projects it.
            UserProfileComment comment = new()
            {
                ProfileUserId = _reporterId,
                UserId = commenterId,
                CommentText = "seeded comment",
                DatePosted = DateTime.UtcNow,
            };
            seedDb.UserProfileComments.Add(comment);
            await seedDb.SaveChangesAsync();
            commentId = comment.CommentId;
        }

        long reportId = await SeedReportAsync(ReportedEntityType.Comment, commentId, _reporterId);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await GetMod().ApplyAccountActionAsync(reportId, ModeratorActionType.BanUser, "Abusive comment.");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        User commenter = await db.Users.SingleAsync(u => u.Id == commenterId);
        commenter.AccountStatus.Should().Be(AccountStatusEnum.Banned);
    }

    [Fact]
    public async Task ApplyAccountActionAsync_DecrementsTargetActiveReportCount()
    {
        int authorId = await SeedUserAsync("CountAuthor");
        int storyId = await SeedStoryAsync(authorId);
        long reportId = await SeedReportAsync(ReportedEntityType.Story, storyId, _reporterId);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await GetMod().ApplyAccountActionAsync(reportId, ModeratorActionType.WarnUser, "Warned.");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Story story = await db.Stories.IgnoreQueryFilters(["IsTakenDown"])
            .SingleAsync(s => s.StoryId == storyId);
        story.ActiveReportCount.Should().Be(0,
            "an account action resolves the report, so it decrements like the other two resolve " +
            "paths — it never did, which leaked the counter the triage sort orders on");
    }

    [Fact]
    public async Task ApplyAccountActionAsync_WhenAuthorCannotBeResolved_ThrowsUserFacing()
    {
        // A report pointing at a story id that does not exist: LoadModeratableAsync returns null,
        // so there is no account behind the report.
        long reportId = await SeedReportAsync(ReportedEntityType.Story, 999_999, _reporterId);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        Func<Task> act = () => GetMod().ApplyAccountActionAsync(
            reportId, ModeratorActionType.WarnUser, "No one to warn.");

        // ModerationValidationException : CanalaveValidationException — ExceptionPresenter surfaces
        // its message verbatim, where the InvalidOperationException it replaced was flattened into
        // "Something went wrong on our end."
        await act.Should().ThrowAsync<ModerationValidationException>()
            .WithMessage("*no account to act on*");
    }

    [Fact]
    public async Task ApplyAccountActionToUserAsync_FilesAModeratorInitiatedReport_AndSetsStatus()
    {
        int targetId = await SeedUserAsync("NeverReported");
        short reasonId = await GetFirstReasonIdAsync();

        // Relative to now: a suspension's end date must be in the future (service §2.1.3(a)).
        DateTime until = DateTime.UtcNow.Date.AddDays(60);
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await GetMod().ApplyAccountActionToUserAsync(
            targetId, reasonId, ModeratorActionType.SuspendUser, "Ban evasion.", until);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        User target = await db.Users.SingleAsync(u => u.Id == targetId);
        target.AccountStatus.Should().Be(AccountStatusEnum.Suspended);
        target.SuspendedUntilUtc.Should().Be(until);

        Report report = await db.Reports.SingleAsync(r =>
            r.ReportedEntityType == ReportedEntityType.User && r.ReportedEntityId == targetId);
        report.ReporterUserId.Should().Be(_modId);
        report.ReportedUserId.Should().Be(targetId, "D8: every producer records who the report is about");
        report.ModeratorUserId.Should().Be(_modId,
            "ReporterUserId == ModeratorUserId is what marks a report as moderator-initiated — " +
            "there is no separate moderation-action table");
        report.ReportStatusId.Should().Be(ReportStatusEnum.ResolvedActionTaken);
        report.ActionTaken.Should().Be("Ban evasion.");
        report.DateResolved.Should().NotBeNull();

        target.ActiveReportCount.Should().Be(0,
            "the row opens and resolves in one step, so the +1/-1 pair every other path makes would cancel");
    }

    [Fact]
    public async Task ApplyAccountActionToUserAsync_AsNonModerator_Throws()
    {
        int targetId = await SeedUserAsync("SomeoneElse");
        short reasonId = await GetFirstReasonIdAsync();

        SetActiveUser(_reporterId); // plain user, not moderator
        Func<Task> act = () => GetMod().ApplyAccountActionToUserAsync(
            targetId, reasonId, ModeratorActionType.BanUser, "Not allowed.");

        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*Moderator*");
    }

    [Fact]
    public async Task ApplyAccountActionToUserAsync_OnSelf_Throws()
    {
        short reasonId = await GetFirstReasonIdAsync();

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        Func<Task> act = () => GetMod().ApplyAccountActionToUserAsync(
            _modId, reasonId, ModeratorActionType.BanUser, "Oops.");

        await act.Should().ThrowAsync<ModerationValidationException>().WithMessage("*yourself*");
    }

    // ── GetUserModerationHistoryAsync (WU-UserModeration) ─────────────────────────

    [Fact]
    public async Task GetUserModerationHistoryAsync_ReturnsStanding_AndReportsAboutTheUserAndTheirContent()
    {
        int targetId = await SeedUserAsync("HistorySubject");
        await SeedReportAsync(ReportedEntityType.User, targetId, _reporterId, reportedUserId: targetId);

        // A report against a story this user wrote NOW appears (owner ruling D8, tracker B18): the
        // history reads ReportedUserId across every target type. It used to be user-targeted only.
        int storyId = await SeedStoryAsync(targetId);
        await SeedReportAsync(ReportedEntityType.Story, storyId, _reporterId, reportedUserId: targetId);
        string storyTitle;
        using (IServiceScope lookup = Factory.Services.CreateScope())
            storyTitle = await lookup.ServiceProvider.GetRequiredService<ApplicationDbContext>().StoryListings
                .Where(l => l.StoryId == storyId).Select(l => l.StoryTitle).SingleAsync();

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        UserModerationHistoryDto? history = await GetMod().GetUserModerationHistoryAsync(targetId);

        history.Should().NotBeNull();
        history!.UserId.Should().Be(targetId);
        // SeedUserAsync appends a per-run GUID suffix to the label — match the prefix, not the whole.
        history.Username.Should().StartWith("HistorySubject");
        history.AccountStatus.Should().Be(AccountStatusEnum.Active);
        history.Reports.Should().HaveCount(2);
        history.Reports.Should().ContainSingle(r => r.EntityType == ReportedEntityType.User);
        history.Reports.Should().ContainSingle(r => r.EntityType == ReportedEntityType.Story
                                                    && r.TargetLabel == storyTitle,
            "a content report is labelled with its target, like the queue's rows");
    }

    [Fact]
    public async Task GetUserModerationHistoryAsync_UnknownUser_ReturnsNull()
    {
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        (await GetMod().GetUserModerationHistoryAsync(999_999)).Should().BeNull();
    }

    // ── Story approval guards + trust record (WU-StoryLifecycle, owner ruling D1) ────────

    [Fact]
    public async Task ApproveStoryAsync_StampsPublishedDate_AndRecordsOneApproval()
    {
        int authorId = await SeedUserAsync("FirstTimer");
        int storyId = await SeedPendingStoryAsync(authorId, StoryStatusEnum.Completed);
        DateTime before = DateTime.UtcNow.AddSeconds(-1);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await GetMod().ApproveStoryAsync(storyId);

        (Story story, User author) = await LoadStoryAndAuthorAsync(storyId, authorId);
        story.StoryStatusId.Should().Be(StoryStatusEnum.Completed);
        story.PublishedDate.Should().NotBeNull().And.BeOnOrAfter(before,
            "D2: the first publication stamps PublishedDate (it was NULL while pending)");
        author.ApprovedStorySubmissions.Should().Be(1);
    }

    [Fact]
    public async Task ApproveStoryAsync_KeepsAnEarlierPublishedDate()
    {
        // A story that was published, pulled back and resubmitted keeps its original date (D2).
        int authorId = await SeedUserAsync("Returning");
        int storyId = await SeedPendingStoryAsync(authorId, StoryStatusEnum.InProgress);
        DateTime original = new(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        await UpdateStoryAsync(storyId, s => s.SetProperty(x => x.PublishedDate, original));

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await GetMod().ApproveStoryAsync(storyId);

        (Story story, _) = await LoadStoryAndAuthorAsync(storyId, authorId);
        story.PublishedDate.Should().Be(original, "republication is not a publication event");
    }

    [Fact]
    public async Task ApproveStoryAsync_Twice_SecondThrows_AndCountsOnce()
    {
        int authorId = await SeedUserAsync("DoubleApprove");
        int storyId = await SeedPendingStoryAsync(authorId, StoryStatusEnum.InProgress);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await GetMod().ApproveStoryAsync(storyId);
        Func<Task> second = () => GetMod().ApproveStoryAsync(storyId);

        await second.Should().ThrowAsync<ModerationValidationException>().WithMessage("*already handled*");
        (_, User author) = await LoadStoryAndAuthorAsync(storyId, authorId);
        author.ApprovedStorySubmissions.Should().Be(1, "the trust record counts a moderator decision once");
    }

    [Fact]
    public async Task ApproveStoryAsync_AfterAuthorWithdraws_Throws_AndCountsNothing()
    {
        int authorId = await SeedUserAsync("Withdrawer");
        int storyId = await SeedPendingStoryAsync(authorId, StoryStatusEnum.InProgress);

        SetActiveUser(authorId);
        await GetStoryWrite().TransitionStatusAsync(storyId, StoryStatusEnum.Draft);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        Func<Task> approve = () => GetMod().ApproveStoryAsync(storyId);

        await approve.Should().ThrowAsync<ModerationValidationException>().WithMessage("*already handled*");
        (Story story, User author) = await LoadStoryAndAuthorAsync(storyId, authorId);
        story.StoryStatusId.Should().Be(StoryStatusEnum.Draft);
        author.ApprovedStorySubmissions.Should().Be(0);
    }

    [Theory]
    [InlineData(StoryStatusEnum.Draft)]     // the approve-into-Draft hole
    [InlineData(StoryStatusEnum.OnHiatus)]  // defined, but not an entry status
    public async Task ApproveStoryAsync_NonEntryPostApprovalStatus_Throws_StatusUnchanged(StoryStatusEnum postApproval)
    {
        int authorId = await SeedUserAsync("BadTarget");
        int storyId = await SeedPendingStoryAsync(authorId, postApproval);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        Func<Task> approve = () => GetMod().ApproveStoryAsync(storyId);

        await approve.Should().ThrowAsync<ModerationValidationException>().WithMessage("*Reject it*");
        (Story story, User author) = await LoadStoryAndAuthorAsync(storyId, authorId);
        story.StoryStatusId.Should().Be(StoryStatusEnum.PendingApproval);
        author.ApprovedStorySubmissions.Should().Be(0);
    }

    [Theory]
    [InlineData(AccountStatusEnum.Banned, null)]
    [InlineData(AccountStatusEnum.Suspended, 30)]   // suspended into the future
    [InlineData(AccountStatusEnum.Suspended, null)] // null end date — deliberately treated as live-suspended
    public async Task ApproveStoryAsync_NonLiveAuthor_Throws_ButRejectStillWorks(
        AccountStatusEnum status, int? suspendedDays)
    {
        int authorId = await SeedUserAsync("NotLive");
        int storyId = await SeedPendingStoryAsync(authorId, StoryStatusEnum.InProgress);
        DateTime? until = suspendedDays is int d ? DateTime.UtcNow.AddDays(d) : null;
        await UpdateUserAsync(authorId, u => u
            .SetProperty(x => x.AccountStatus, status)
            .SetProperty(x => x.SuspendedUntilUtc, until));

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        Func<Task> approve = () => GetMod().ApproveStoryAsync(storyId);
        await approve.Should().ThrowAsync<ModerationValidationException>().WithMessage("*reject it instead*");

        await GetMod().RejectStoryAsync(storyId, "Author not in good standing.");
        (Story story, _) = await LoadStoryAndAuthorAsync(storyId, authorId);
        story.StoryStatusId.Should().Be(StoryStatusEnum.Rejected, "reject is unguarded so the queue can always clear");
    }

    [Fact]
    public async Task ApproveStoryAsync_SuspensionAlreadyEnded_IsLive()
    {
        int authorId = await SeedUserAsync("ServedTime");
        int storyId = await SeedPendingStoryAsync(authorId, StoryStatusEnum.InProgress);
        await UpdateUserAsync(authorId, u => u
            .SetProperty(x => x.AccountStatus, AccountStatusEnum.Suspended)
            .SetProperty(x => x.SuspendedUntilUtc, DateTime.UtcNow.AddDays(-1)));

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await GetMod().ApproveStoryAsync(storyId);

        (Story story, _) = await LoadStoryAndAuthorAsync(storyId, authorId);
        story.StoryStatusId.Should().Be(StoryStatusEnum.InProgress);
    }

    [Fact]
    public async Task ApproveStoryAsync_DeletedAuthor_Throws()
    {
        // D13 hard delete leaves the FK SetNull — an anonymized story can't be approved.
        int storyId = await SeedPendingStoryAsync(await SeedUserAsync("Gone"), StoryStatusEnum.InProgress);
        await UpdateStoryAsync(storyId, s => s.SetProperty(x => x.AuthorId, (int?)null));

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        Func<Task> approve = () => GetMod().ApproveStoryAsync(storyId);

        await approve.Should().ThrowAsync<ModerationValidationException>().WithMessage("*deleted*");
    }

    [Fact]
    public async Task ApproveAndReject_UnknownStory_ThrowKeyNotFound()
    {
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));

        await GetMod().Invoking(m => m.ApproveStoryAsync(999_999)).Should().ThrowAsync<KeyNotFoundException>();
        await GetMod().Invoking(m => m.RejectStoryAsync(999_999, "x")).Should().ThrowAsync<KeyNotFoundException>();
    }

    [Theory]
    [InlineData(StoryStatusEnum.Draft)]
    [InlineData(StoryStatusEnum.InProgress)] // published work is removed by takedown, never rejected
    [InlineData(StoryStatusEnum.Rejected)]
    public async Task RejectStoryAsync_NotPending_Throws(StoryStatusEnum status)
    {
        int storyId = await SeedStoryAsync(await SeedUserAsync("NotPending"), status: status);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        Func<Task> reject = () => GetMod().RejectStoryAsync(storyId, "no");

        await reject.Should().ThrowAsync<ModerationValidationException>().WithMessage("*already handled*");
        (Story story, _) = await LoadStoryAndAuthorAsync(storyId, null);
        story.StoryStatusId.Should().Be(status);
    }

    [Fact]
    public async Task GetPendingSubmissionsAsync_OrdersBySubmittedDate_AndReturnsIt()
    {
        int authorId = await SeedUserAsync("QueueOrder");
        DateTime older = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime newer = new(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc);
        int newerId = await SeedPendingStoryAsync(authorId, StoryStatusEnum.InProgress, submittedDate: newer);
        int olderId = await SeedPendingStoryAsync(authorId, StoryStatusEnum.InProgress, submittedDate: older);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        StorySubmissionQueueItemDto[] queue = await GetMod().GetPendingSubmissionsAsync();

        queue.Select(q => q.StoryId).Should().Equal(olderId, newerId);
        queue[0].SubmittedDate.Should().Be(older);
    }

    [Fact]
    public async Task SetCanAutoApproveAsync_Revoke_WritesFlagAndAModeratorInitiatedReport()
    {
        int targetId = await SeedUserAsync("TrustedAuthor");
        short reasonId = await GetFirstReasonIdAsync();

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await GetMod().SetCanAutoApproveAsync(targetId, canAutoApprove: false, reasonId, "Spam after approval.");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        User target = await db.Users.SingleAsync(u => u.Id == targetId);
        target.CanAutoApprove.Should().BeFalse();
        target.ActiveReportCount.Should().Be(0, "the audit row opens and resolves together");

        Report report = await db.Reports.SingleAsync(r =>
            r.ReportedEntityType == ReportedEntityType.User && r.ReportedEntityId == targetId);
        report.ReporterUserId.Should().Be(_modId);
        report.ReportedUserId.Should().Be(targetId, "D8: every producer records who the report is about");
        report.ModeratorUserId.Should().Be(_modId, "ReporterUserId == ModeratorUserId marks it moderator-initiated");
        report.ReportStatusId.Should().Be(ReportStatusEnum.ResolvedActionTaken);
        report.ActionTaken.Should().Be("Auto-approve revoked: Spam after approval.");
    }

    [Fact]
    public async Task SetCanAutoApproveAsync_UnchangedValue_WritesNothing()
    {
        int targetId = await SeedUserAsync("AlreadyOn");
        short reasonId = await GetFirstReasonIdAsync();

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await GetMod().SetCanAutoApproveAsync(targetId, canAutoApprove: true, reasonId, "Restoring.");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Reports.AnyAsync(r => r.ReportedEntityId == targetId)).Should().BeFalse();
    }

    [Fact]
    public async Task SetCanAutoApproveAsync_NonModerator_And_Self_AreRefused()
    {
        int targetId = await SeedUserAsync("Target");
        short reasonId = await GetFirstReasonIdAsync();

        SetActiveUser(_reporterId);
        await GetMod().Invoking(m => m.SetCanAutoApproveAsync(targetId, false, reasonId, "x"))
            .Should().ThrowAsync<UnauthorizedAccessException>();

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await GetMod().Invoking(m => m.SetCanAutoApproveAsync(_modId, false, reasonId, "x"))
            .Should().ThrowAsync<ModerationValidationException>().WithMessage("*your own*");
    }

    [Fact]
    public async Task SetCanAutoApproveAsync_RevokeThenRestore_WritesTwoAuditRows_AndTheWaiverReturns()
    {
        int authorId = await SeedUserAsync("RestoredAuthor");
        await UpdateUserAsync(authorId, u => u.SetProperty(x => x.ApprovedStorySubmissions, 1));
        short reasonId = await GetFirstReasonIdAsync();

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await GetMod().SetCanAutoApproveAsync(authorId, canAutoApprove: false, reasonId, "Spam after approval.");
        await GetMod().SetCanAutoApproveAsync(authorId, canAutoApprove: true, reasonId, "Cleared on appeal.");

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Users.SingleAsync(u => u.Id == authorId)).CanAutoApprove.Should().BeTrue();
            List<string?> actions = await db.Reports
                .Where(r => r.ReportedEntityType == ReportedEntityType.User && r.ReportedEntityId == authorId)
                .OrderBy(r => r.ReportId)
                .Select(r => r.ActionTaken)
                .ToListAsync();
            actions.Should().Equal("Auto-approve revoked: Spam after approval.", "Auto-approve restored: Cleared on appeal.");
        }

        // Restored for real: the author's next submit takes the trust waiver again.
        int storyId = await SeedStoryAsync(authorId, status: StoryStatusEnum.Draft);
        SetActiveUser(authorId);
        (await GetStoryWrite().TransitionStatusAsync(storyId, StoryStatusEnum.PendingApproval))
            .Should().Be(StoryStatusEnum.InProgress, "a restored author publishes directly again");
    }

    [Fact]
    public async Task SetCanAutoApproveAsync_UnknownUser_UnknownReason_AndOverLongReason_AreRefused()
    {
        int targetId = await SeedUserAsync("Guarded");
        short reasonId = await GetFirstReasonIdAsync();
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));

        await GetMod().Invoking(m => m.SetCanAutoApproveAsync(999_999, false, reasonId, "x"))
            .Should().ThrowAsync<KeyNotFoundException>();
        await GetMod().Invoking(m => m.SetCanAutoApproveAsync(targetId, false, short.MaxValue, "x"))
            .Should().ThrowAsync<ModerationValidationException>().WithMessage("*reason*");
        await GetMod().Invoking(m => m.SetCanAutoApproveAsync(targetId, false, reasonId, new string('r', 1100)))
            .Should().ThrowAsync<ModerationValidationException>().WithMessage("*too long*");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Users.SingleAsync(u => u.Id == targetId)).CanAutoApprove.Should().BeTrue("every refusal writes nothing");
        (await db.Reports.AnyAsync(r => r.ReportedEntityId == targetId)).Should().BeFalse();
    }

    // ── Takedown freezes status: approve/reject refuse it, the queue hides it ─────

    [Fact]
    public async Task ApproveAndReject_TakenDownPendingStory_AreRefused_AndTheQueueHidesIt()
    {
        int authorId = await SeedUserAsync("TakenDownPending");
        int storyId = await SeedPendingStoryAsync(authorId, StoryStatusEnum.InProgress);
        DateTime takedownDate = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        await UpdateStoryAsync(storyId, s => s
            .SetProperty(x => x.IsTakenDown, true)
            .SetProperty(x => x.TakedownReason, "Spam takedown.")
            .SetProperty(x => x.TakedownDate, takedownDate));

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        (await GetMod().GetPendingSubmissionsAsync()).Should().NotContain(q => q.StoryId == storyId,
            "a taken-down story's status is frozen, so the queue would only offer two dead buttons");

        await GetMod().Invoking(m => m.ApproveStoryAsync(storyId))
            .Should().ThrowAsync<ModerationValidationException>().WithMessage("*takedown*");
        await GetMod().Invoking(m => m.RejectStoryAsync(storyId, "Rejected on top of the takedown."))
            .Should().ThrowAsync<ModerationValidationException>().WithMessage("*takedown*");

        (Story story, User author) = await LoadStoryAndAuthorAsync(storyId, authorId);
        story.StoryStatusId.Should().Be(StoryStatusEnum.PendingApproval);
        story.TakedownReason.Should().Be("Spam takedown.", "a reject must not overwrite the takedown's own reason");
        story.TakedownDate.Should().Be(takedownDate);
        author.ApprovedStorySubmissions.Should().Be(0, "a taken-down story never earns trust");
    }

    // ── The conditional writes themselves, not the pre-read check (InterleavingCommandInterceptor) ──

    [Fact]
    public async Task ApproveStoryAsync_StatusChangesBetweenReadAndWrite_Throws_AndCountsNothing()
    {
        // The author withdraws AFTER approve's pre-read passed and BEFORE its conditional update —
        // the race the sequential "approve after withdraw" test can't reach.
        int authorId = await SeedUserAsync("WithdrawsMidApprove");
        int storyId = await SeedPendingStoryAsync(authorId, StoryStatusEnum.InProgress);
        InterleavingCommandInterceptor authorWithdraws = new(ConnectionString, "UPDATE stories",
            $"UPDATE stories SET story_status_id = {(int)StoryStatusEnum.Draft} WHERE story_id = {storyId}");

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await ActThroughInterceptorAsync(authorWithdraws, mod => mod
            .Invoking(m => m.ApproveStoryAsync(storyId))
            .Should().ThrowAsync<ModerationValidationException>().WithMessage("*already handled*"));

        authorWithdraws.Fired.Should().BeTrue("the competing write must land between the read and the write");
        (Story story, User author) = await LoadStoryAndAuthorAsync(storyId, authorId);
        story.StoryStatusId.Should().Be(StoryStatusEnum.Draft, "the guarded WHERE must not overwrite the withdraw");
        story.PublishedDate.Should().BeNull();
        author.ApprovedStorySubmissions.Should().Be(0, "0 rows affected → nothing incremented");
    }

    [Fact]
    public async Task ApproveStoryAsync_TrustRecordWriteFails_RollsBackTheStatusFlip()
    {
        // The flip and the +1 share one transaction because the trust record has no recompute to heal
        // a split (layer2-services.md §"Records of a decision are not counters").
        int authorId = await SeedUserAsync("SplitApprove");
        int storyId = await SeedPendingStoryAsync(authorId, StoryStatusEnum.Completed);
        InterleavingCommandInterceptor trustWriteFails = new(ConnectionString, "UPDATE \"AspNetUsers\"",
            failWith: new InvalidOperationException("Simulated failure writing the trust record."));

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await ActThroughInterceptorAsync(trustWriteFails, mod => mod
            .Invoking(m => m.ApproveStoryAsync(storyId))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("Simulated*"));

        trustWriteFails.Fired.Should().BeTrue();
        (Story story, User author) = await LoadStoryAndAuthorAsync(storyId, authorId);
        story.StoryStatusId.Should().Be(StoryStatusEnum.PendingApproval, "the status flip rolls back with the failed +1");
        story.PublishedDate.Should().BeNull();
        author.ApprovedStorySubmissions.Should().Be(0);
    }

    [Fact]
    public async Task RejectStoryAsync_StatusChangesBetweenReadAndWrite_Throws_AndWritesNoReason()
    {
        // Another moderator approves between this reject's read and its conditional update.
        int authorId = await SeedUserAsync("ApprovedMidReject");
        int storyId = await SeedPendingStoryAsync(authorId, StoryStatusEnum.InProgress);
        InterleavingCommandInterceptor otherModApproves = new(ConnectionString, "UPDATE stories",
            $"UPDATE stories SET story_status_id = {(int)StoryStatusEnum.InProgress}, published_date = now() " +
            $"WHERE story_id = {storyId}");

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await ActThroughInterceptorAsync(otherModApproves, mod => mod
            .Invoking(m => m.RejectStoryAsync(storyId, "Too late."))
            .Should().ThrowAsync<ModerationValidationException>().WithMessage("*already handled*"));

        otherModApproves.Fired.Should().BeTrue();
        (Story story, _) = await LoadStoryAndAuthorAsync(storyId, authorId);
        story.StoryStatusId.Should().Be(StoryStatusEnum.InProgress, "published work is never rejected (D1)");
        story.TakedownReason.Should().BeNull("the losing reject writes nothing");
    }

    [Fact]
    public async Task GetUserModerationHistoryAsync_CarriesTheTrustFields()
    {
        int targetId = await SeedUserAsync("TrustFields");
        await UpdateUserAsync(targetId, u => u
            .SetProperty(x => x.ApprovedStorySubmissions, 2)
            .SetProperty(x => x.CanAutoApprove, false));

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        UserModerationHistoryDto history = (await GetMod().GetUserModerationHistoryAsync(targetId))!;

        history.ApprovedStorySubmissions.Should().Be(2);
        history.CanAutoApprove.Should().BeFalse();
    }

    [Fact]
    public async Task NewUser_DefaultsToCanAutoApprove_WithNoApprovals()
    {
        // Pins the `= true` CLR initializer, which is the load-bearing half of a true-default bool:
        // the sentinel is true, so a new User without the initializer would INSERT an explicit false
        // (mutation-checked 2026-09-30). The explicit-false path is the next test.
        int userId = await SeedUserAsync("Fresh");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        User user = await db.Users.SingleAsync(u => u.Id == userId);
        user.CanAutoApprove.Should().BeTrue();
        user.ApprovedStorySubmissions.Should().Be(0);
    }

    [Fact]
    public async Task NewUser_InsertedWithCanAutoApproveFalse_KeepsFalse()
    {
        // An explicit false must survive the INSERT. EF omits a property whose value equals its
        // sentinel and lets the DB default (true) fill it, so this holds only while the sentinel is
        // true. EF Core 10 already infers that from HasDefaultValue(true) — HasSentinel(true) states
        // it explicitly — so this pins the behavior, whichever of the two supplies it
        // (layer1-data-model.md §"Column Conventions", true-default bools).
        int userId;
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            UserManager<User> users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            string suffix = Guid.NewGuid().ToString("N")[..8];
            User user = new()
            {
                UserName = $"Untrusted-{suffix}",
                Email = $"untrusted-{suffix}@test.invalid",
                EmailConfirmed = true,
                ThemeId = 1,
                CanAutoApprove = false,
            };
            (await users.CreateAsync(user, "Password123!")).Succeeded.Should().BeTrue();
            userId = user.Id;
        }

        using IServiceScope readScope = Factory.Services.CreateScope();
        ApplicationDbContext db = readScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Users.SingleAsync(u => u.Id == userId)).CanAutoApprove.Should().BeFalse();
    }

    public override async Task DisposeAsync()
    {
        foreach (IServiceScope scope in _serviceScopes)
            scope.Dispose();
        await base.DisposeAsync();
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private IModerationWriteService GetMod()
    {
        IServiceScope scope = Factory.Services.CreateScope();
        _serviceScopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<IModerationWriteService>();
    }

    /// <summary>The member-facing submission interface (owner ruling D9's split).</summary>
    private IReportSubmissionService GetSubmission()
    {
        IServiceScope scope = Factory.Services.CreateScope();
        _serviceScopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<IReportSubmissionService>();
    }

    /// <summary>
    /// Runs <paramref name="act"/> against a <see cref="ServerModerationWriteService"/> whose write
    /// context carries <paramref name="interceptor"/>; disposes both afterwards.
    /// </summary>
    private async Task ActThroughInterceptorAsync(InterleavingCommandInterceptor interceptor,
        Func<ServerModerationWriteService, Task> act)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerModerationWriteService mod, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerModerationWriteService>(scope, ConnectionString, interceptor);
        await using (writeDb)
            await act(mod);
    }

    private IStoryWriteService GetStoryWrite()
    {
        IServiceScope scope = Factory.Services.CreateScope();
        _serviceScopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<IStoryWriteService>();
    }

    private async Task<(Story Story, User Author)> LoadStoryAndAuthorAsync(int storyId, int? authorId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Story story = await db.Stories.SingleAsync(s => s.StoryId == storyId);
        User author = authorId is int id ? await db.Users.SingleAsync(u => u.Id == id) : null!;
        return (story, author);
    }

    private async Task UpdateStoryAsync(int storyId,
        Action<Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<Story>> setters)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Stories.Where(s => s.StoryId == storyId).ExecuteUpdateAsync(setters);
    }

    private async Task UpdateUserAsync(int userId,
        Action<Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<User>> setters)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(setters);
    }

    private async Task<short> GetFirstReasonIdAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.ReportReasons.OrderBy(r => r.ReportReasonId)
            .Select(r => r.ReportReasonId)
            .FirstAsync();
    }

    /// <summary>
    /// Inserts a report row for the given target and returns the <c>ReportId</c>.
    /// Increments the target's <c>ActiveReportCount</c> inline so the report queue
    /// returns a meaningful count.
    /// </summary>
    private async Task<long> SeedReportAsync(ReportedEntityType type, long entityId, int reporterId,
        int? reportedUserId = null)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        short reasonId = await db.ReportReasons.OrderBy(r => r.ReportReasonId)
            .Select(r => r.ReportReasonId).FirstAsync();

        Report report = new()
        {
            ReportedEntityType = type,
            ReportedEntityId = entityId,
            ReportReasonId = reasonId,
            ReporterUserId = reporterId,
            ReportedUserId = reportedUserId,
            ReportStatusId = ReportStatusEnum.Open,
            DateReported = DateTime.UtcNow,
        };
        db.Reports.Add(report);

        // Increment target count inline (mirrors what SubmitReportAsync does via ExecuteUpdate).
        if (type == ReportedEntityType.Story)
            await db.Stories.IgnoreQueryFilters(["IsTakenDown"])
                .Where(s => s.StoryId == (int)entityId)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.ActiveReportCount, s => s.ActiveReportCount + 1));

        await db.SaveChangesAsync();
        return report.ReportId;
    }

    /// <summary>
    /// Seeds a story in <see cref="StoryStatusEnum.PendingApproval"/> with the given
    /// <paramref name="postApprovalStatus"/> and returns the <c>StoryId</c>.
    /// </summary>
    private async Task<int> SeedPendingStoryAsync(int authorId, StoryStatusEnum postApprovalStatus,
        Rating rating = Rating.E, DateTime? submittedDate = null)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        string suffix = Guid.NewGuid().ToString("N")[..8];
        Story story = new()
        {
            AuthorId = authorId,
            Rating = rating,
            StoryStatusId = StoryStatusEnum.PendingApproval,
            // D2: a pending story has never been published; the queue's date is SubmittedDate.
            PublishedDate = null,
            SubmittedDate = submittedDate ?? DateTime.UtcNow,
            LastUpdatedDate = DateTime.UtcNow,
            StoryListing = new StoryListing { StoryTitle = $"Pending Story {suffix}", ShortDescription = "test" },
            StoryDetail = new StoryDetail { LongDescription = "test", PostApprovalStatus = postApprovalStatus },
        };
        db.Stories.Add(story);
        await db.SaveChangesAsync();
        return story.StoryId;
    }
}
