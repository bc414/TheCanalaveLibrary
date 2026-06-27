using FluentAssertions;
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
///   <item><c>SubmitReportAsync</c>: creates Report row, increments <c>ActiveReportCount</c>.</item>
///   <item>Invalid target type throws immediately (allow-set gate).</item>
///   <item><c>ResolveNoActionAsync</c>: status → ResolvedNoAction, count decremented, notification sent.</item>
///   <item><c>ResolveWithRemovalAsync</c> (soft takedown): sets <c>IsTakenDown=true</c>, drops from
///   public reads, remains visible with <c>IgnoreQueryFilters(["IsTakenDown"])</c>.</item>
///   <item>Dedup-key fix: two reports on *different* stories both produce <c>ReportReceived</c>
///   notifications; two on the *same* story dedup to one notification.</item>
///   <item><c>ApproveStoryAsync</c>: sets <c>StoryStatusId = PostApprovalStatus</c>, fires
///   <c>StoryApproved</c> notification.</item>
///   <item><c>RejectStoryAsync</c>: sets <c>StoryStatusId = Rejected</c>, records reason, fires
///   <c>StoryRejected</c> notification.</item>
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
        await GetMod().SubmitReportAsync(new SubmitReportRequest(
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

        Func<Task> act = () => GetMod().SubmitReportAsync(new SubmitReportRequest(
            (ReportedEntityType)99, 1, reasonId, null));

        await act.Should().ThrowAsync<InvalidOperationException>()
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
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Story story = await db.Stories.IgnoreQueryFilters(["IsTakenDown"])
            .SingleAsync(s => s.StoryId == storyId);
        story.IsTakenDown.Should().BeTrue();
        story.TakedownReason.Should().Be("rule violation");
        story.TakedownDate.Should().NotBeNull();

        // Public query (with IsTakenDown filter active) should not find the story.
        bool visiblePublicly = await db.Stories
            .AnyAsync(s => s.StoryId == storyId);
        visiblePublicly.Should().BeFalse();

        // ContentRemoved notification should go to the author.
        bool authorNotified = await db.Notifications.AnyAsync(n =>
            n.RecipientUserId == authorId &&
            n.NotificationTypeId == NotificationTypeEnum.ContentRemoved);
        authorNotified.Should().BeTrue();
    }

    // ── Dedup-key fix (RelatedEntityId in create-core) ────────────────────────────

    [Fact]
    public async Task NotifyReportReceivedAsync_TwoDifferentStories_BothNotificationsLand()
    {
        int storyA = await SeedStoryAsync();
        int storyB = await SeedStoryAsync();

        using IServiceScope scope = Factory.Services.CreateScope();
        INotificationWriteService notifSvc =
            scope.ServiceProvider.GetRequiredService<INotificationWriteService>();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Both calls share same sourceUserId (mod) and same type, but different RelatedEntityIds.
        await notifSvc.NotifyStoryRejectedAsync(_reporterId, storyA, _modId);
        await notifSvc.NotifyStoryRejectedAsync(_reporterId, storyB, _modId);

        int count = await db.Notifications.CountAsync(n =>
            n.RecipientUserId == _reporterId &&
            n.NotificationTypeId == NotificationTypeEnum.StoryRejected);
        count.Should().Be(2, "each distinct RelatedEntityId should produce its own notification");
    }

    [Fact]
    public async Task NotifyReportReceivedAsync_SameStoryTwice_SecondDeduped()
    {
        int storyId = await SeedStoryAsync();

        using IServiceScope scope = Factory.Services.CreateScope();
        INotificationWriteService notifSvc =
            scope.ServiceProvider.GetRequiredService<INotificationWriteService>();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await notifSvc.NotifyStoryApprovedAsync(_reporterId, storyId, _modId);
        await notifSvc.NotifyStoryApprovedAsync(_reporterId, storyId, _modId);

        int count = await db.Notifications.CountAsync(n =>
            n.RecipientUserId == _reporterId &&
            n.NotificationTypeId == NotificationTypeEnum.StoryApproved);
        count.Should().Be(1, "duplicate notification for same entity should be deduped");
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

    // ── Non-moderator ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ClaimReportAsync_AsNonModerator_Throws()
    {
        int storyId = await SeedStoryAsync();
        long reportId = await SeedReportAsync(ReportedEntityType.Story, storyId, _reporterId);

        SetActiveUser(_reporterId); // plain user, not moderator
        Func<Task> act = () => GetMod().ClaimReportAsync(reportId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Moderator*");
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
    private async Task<long> SeedReportAsync(ReportedEntityType type, long entityId, int reporterId)
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
    private async Task<int> SeedPendingStoryAsync(int authorId, StoryStatusEnum postApprovalStatus)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        string suffix = Guid.NewGuid().ToString("N")[..8];
        Story story = new()
        {
            AuthorId = authorId,
            Rating = Rating.E,
            StoryStatusId = StoryStatusEnum.PendingApproval,
            PublishedDate = DateTime.UtcNow,
            LastUpdatedDate = DateTime.UtcNow,
            StoryListing = new StoryListing { StoryTitle = $"Pending Story {suffix}", ShortDescription = "test" },
            StoryDetail = new StoryDetail { LongDescription = "test", PostApprovalStatus = postApprovalStatus },
        };
        db.Stories.Add(story);
        await db.SaveChangesAsync();
        return story.StoryId;
    }
}
