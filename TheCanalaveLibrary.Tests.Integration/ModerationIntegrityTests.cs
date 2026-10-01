using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// The report-lifecycle integrity rules WU-ModerationIntegrity built (2026-09-30), per owner rulings
/// D7/D8/D9 and service audit §2.1.2/§2.1.3/§2.4.4 — rule text in <c>layer2-services.md</c>
/// §"Moderation Services":
/// <list type="bullet">
///   <item><b>D7 sibling closing</b>: a removal closes every other open report on the same
///   <c>(type, id)</c> target (soft and hard), the counter moves by the rows closed, every sibling
///   reporter hears it with their own report id; no-action and account actions close nothing; a
///   same-numbered target of another type is untouched; a claimed sibling closes; a Message target
///   closes its siblings with no counter.</item>
///   <item><b>§2.1.2 status guards</b>: a resolved report cannot be resolved again (any path), an
///   unknown id is <see cref="KeyNotFoundException"/>, two racing resolves have exactly one winner, a
///   User report cannot be removed.</item>
///   <item><b>§2.4.4 submission</b>: one open report per reporter per target (service check and the
///   index's race), a resolved report can be re-filed, anonymous reports are not deduped, the counter
///   moves once per row.</item>
///   <item><b>D8 <c>ReportedUserId</c></b> for every target type, NULL for authorless content, nulled
///   (not deleted) when the account goes; the history reads it and keeps deleted targets.</item>
///   <item><b>Zombie closure at the source</b> (D7 sub-edge): account deletion closes the reports on
///   what it destroys, silently, and leaves reports on surviving content open.</item>
///   <item><b>§2.1.3 account-status table</b> and Reinstate; the report-driven action tells the member
///   reporter (81).</item>
///   <item><b>D9 read gates</b>: every moderator-only read refuses a signed-in non-moderator (403 type)
///   and an anonymous caller (401 type) on the service itself — the tier that catches a circuit-path
///   regression.</item>
/// </list>
/// FK parents: users via <see cref="IntegrationTestBase.SeedUserAsync"/>, stories via
/// <see cref="IntegrationTestBase.SeedStoryAsync"/>; comments, blog posts, recommendations and messages
/// (conversation + participants + message) inline; <c>report_reasons</c>/<c>report_statuses</c> are
/// Respawn-ignored seeds. Multi-report tests seed distinct reporters — the partial unique index allows
/// one open report per reporter per target.
/// Tier: <b>Integration</b> (real Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class ModerationIntegrityTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _modId;
    private short _reasonId;
    private readonly List<IServiceScope> _scopes = [];

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _modId = await SeedUserAsync("Mod");
        using IServiceScope scope = Factory.Services.CreateScope();
        _reasonId = await Db(scope).ReportReasons.OrderBy(r => r.ReportReasonId).Select(r => r.ReportReasonId).FirstAsync();
    }

    public override async Task DisposeAsync()
    {
        foreach (IServiceScope scope in _scopes) scope.Dispose();
        await base.DisposeAsync();
    }

    // ── D7: sibling closing on removal ───────────────────────────────────────────

    [Fact]
    public async Task Removal_ClosesEverySiblingOnTheTarget_CounterToZero_EveryReporterHearsIt()
    {
        int authorId = await SeedUserAsync("Author");
        int storyId = await SeedStoryAsync(authorId);
        (int r1, int r2, int r3) = (await SeedUserAsync("R1"), await SeedUserAsync("R2"), await SeedUserAsync("R3"));
        long primary = await SubmitAsAsync(r1, ReportedEntityType.Story, storyId);
        long sibling2 = await SubmitAsAsync(r2, ReportedEntityType.Story, storyId);
        long sibling3 = await SubmitAsAsync(r3, ReportedEntityType.Story, storyId);
        (await StoryCounterAsync(storyId)).Should().Be(3);

        // A sibling claimed by another moderator still closes (a claim is triage bookkeeping, not a lock).
        int otherModId = await SeedUserAsync("OtherMod");
        SetActiveUser(FakeActiveUserContext.Moderator(otherModId));
        await Mod().ClaimReportAsync(sibling3);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await Mod().ResolveWithRemovalAsync(primary, "Rule 3: spam.");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Db(scope);
        List<Report> rows = await db.Reports.Where(r => r.ReportedEntityId == storyId
                && r.ReportedEntityType == ReportedEntityType.Story).OrderBy(r => r.ReportId).ToListAsync();
        rows.Should().HaveCount(3, "nothing is deleted from the ledger");
        rows.Should().OnlyContain(r => r.ReportStatusId == ReportStatusEnum.ResolvedActionTaken,
            "action WAS taken on their target — 'no action' would be false (D7)");
        rows.Should().OnlyContain(r => r.ModeratorUserId == _modId);
        Report primaryRow = rows.Single(r => r.ReportId == primary);
        rows.Where(r => r.ReportId != primary).Should().OnlyContain(r =>
            r.DateResolved == primaryRow.DateResolved
            && r.ActionTaken == $"Closed with report #{primary}: Rule 3: spam.",
            "a sibling records the same timestamp and names the report that closed it");
        (await StoryCounterAsync(storyId)).Should().Be(0,
            "the count means 'unanswered questions against this target' — a removal answers all of them");

        foreach ((int reporter, long reportId) in new[] { (r1, primary), (r2, sibling2), (r3, sibling3) })
            (await db.Notifications.CountAsync(n => n.RecipientUserId == reporter
                    && n.NotificationTypeId == NotificationTypeEnum.ReportResolved
                    && n.RelatedEntityId == reportId))
                .Should().Be(1, $"reporter {reporter} learns the outcome of their own report #{reportId} (spec §5.21)");
        (await db.Notifications.AnyAsync(n => n.RecipientUserId == authorId
            && n.NotificationTypeId == NotificationTypeEnum.ContentRemoved)).Should().BeTrue();
    }

    [Fact]
    public async Task HardDelete_ClosesTheSiblings_AndTheReportRowsOutliveTheTarget()
    {
        int storyId = await SeedStoryAsync(await SeedUserAsync("Author"));
        long primary = await SubmitAsAsync(await SeedUserAsync("R1"), ReportedEntityType.Story, storyId);
        await SubmitAsAsync(await SeedUserAsync("R2"), ReportedEntityType.Story, storyId);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await Mod().ResolveWithRemovalAsync(primary, "Illegal content.", hardDelete: true);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Db(scope);
        (await db.Stories.AnyAsync(s => s.StoryId == storyId)).Should().BeFalse();
        List<Report> rows = await db.Reports.Where(r => r.ReportedEntityType == ReportedEntityType.Story
            && r.ReportedEntityId == storyId).ToListAsync();
        rows.Should().HaveCount(2).And.OnlyContain(r => r.ReportStatusId == ReportStatusEnum.ResolvedActionTaken,
            "hard delete is where an unclosed sibling would be an unreachable zombie (D7)");
    }

    [Fact]
    public async Task ResolveNoAction_LeavesTheSiblingsOpen_AndTheCounterAtNMinusOne()
    {
        int storyId = await SeedStoryAsync(await SeedUserAsync("Author"));
        long primary = await SubmitAsAsync(await SeedUserAsync("R1"), ReportedEntityType.Story, storyId);
        await SubmitAsAsync(await SeedUserAsync("R2"), ReportedEntityType.Story, storyId);
        await SubmitAsAsync(await SeedUserAsync("R3"), ReportedEntityType.Story, storyId);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await Mod().ResolveNoActionAsync(primary, "Within the rules.");

        using IServiceScope scope = Factory.Services.CreateScope();
        (await Db(scope).Reports.CountAsync(r => r.ReportedEntityId == storyId
                && r.ReportStatusId == ReportStatusEnum.Open))
            .Should().Be(2, "one moderator's 'no' is a ruling on one complaint (D7)");
        (await StoryCounterAsync(storyId)).Should().Be(2);
    }

    [Fact]
    public async Task AccountAction_ClosesNoSibling()
    {
        int storyId = await SeedStoryAsync(await SeedUserAsync("Author"));
        long primary = await SubmitAsAsync(await SeedUserAsync("R1"), ReportedEntityType.Story, storyId);
        await SubmitAsAsync(await SeedUserAsync("R2"), ReportedEntityType.Story, storyId);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await Mod().ApplyAccountActionAsync(primary, ModeratorActionType.WarnUser, "Warned.");

        using IServiceScope scope = Factory.Services.CreateScope();
        (await Db(scope).Reports.CountAsync(r => r.ReportedEntityId == storyId
            && r.ReportStatusId == ReportStatusEnum.Open)).Should().Be(1,
            "the content stays live, so the other report still asks a live question (D7)");
        (await StoryCounterAsync(storyId)).Should().Be(1);
    }

    [Fact]
    public async Task Removal_LeavesAReportOnTheSameNumberedTargetOfAnotherType_Untouched()
    {
        int storyId = await SeedStoryAsync(await SeedUserAsync("Author"));
        long primary = await SubmitAsAsync(await SeedUserAsync("R1"), ReportedEntityType.Story, storyId);
        // "Comment N" where N is the story's id: reports carry no FK, so the row stands on its own.
        long commentNumbered = await SeedReportRowAsync(ReportedEntityType.Comment, storyId, await SeedUserAsync("R2"));

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await Mod().ResolveWithRemovalAsync(primary, "Removed.");

        using IServiceScope scope = Factory.Services.CreateScope();
        (await Db(scope).Reports.SingleAsync(r => r.ReportId == commentNumbered)).ReportStatusId
            .Should().Be(ReportStatusEnum.Open, "closure is keyed on the (type, id) PAIR — story 5 is not comment 5 (D7)");
    }

    [Fact]
    public async Task Removal_OfAMessage_ClosesItsSiblings_WithNoCounter()
    {
        int senderId = await SeedUserAsync("Sender");
        int recipientId = await SeedUserAsync("Recipient");
        long messageId = await SeedMessageAsync(senderId, recipientId);
        long primary = await SubmitAsAsync(recipientId, ReportedEntityType.Message, messageId);
        long sibling = await SubmitAsAsync(await SeedUserAsync("R2"), ReportedEntityType.Message, messageId);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await Mod().ResolveWithRemovalAsync(primary, "Harassment.");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Db(scope);
        (await db.PrivateMessages.AnyAsync(m => m.MessageId == messageId)).Should().BeFalse(
            "a message has no takedown columns — removal hard-deletes it");
        (await db.Reports.SingleAsync(r => r.ReportId == sibling)).ReportStatusId
            .Should().Be(ReportStatusEnum.ResolvedActionTaken, "Message keeps the sibling half of D7");
    }

    // ── §2.1.2: status guards ────────────────────────────────────────────────────

    [Fact]
    public async Task ResolvingAnAlreadyResolvedReport_IsRefusedOnEveryPath_AndTheCounterHolds()
    {
        int storyId = await SeedStoryAsync(await SeedUserAsync("Author"));
        long report = await SubmitAsAsync(await SeedUserAsync("R1"), ReportedEntityType.Story, storyId);
        await SubmitAsAsync(await SeedUserAsync("R2"), ReportedEntityType.Story, storyId); // keeps the counter at 1 after
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await Mod().ResolveNoActionAsync(report, "Fine.");
        (await StoryCounterAsync(storyId)).Should().Be(1);

        Func<Task>[] again =
        [
            () => Mod().ResolveNoActionAsync(report, "Again."),
            () => Mod().ResolveWithRemovalAsync(report, "Again."),
            () => Mod().ApplyAccountActionAsync(report, ModeratorActionType.WarnUser, "Again."),
        ];
        foreach (Func<Task> act in again)
            await act.Should().ThrowAsync<ModerationValidationException>().WithMessage("*already been resolved*");

        (await StoryCounterAsync(storyId)).Should().Be(1,
            "a second resolution used to decrement again — the counter the triage sort orders on went negative");
    }

    [Fact]
    public async Task ResolvingAnUnknownReport_IsKeyNotFound_OnEveryPath()
    {
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await FluentActions.Invoking(() => Mod().ResolveNoActionAsync(999_999, null)).Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Invoking(() => Mod().ResolveWithRemovalAsync(999_999, "x")).Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Invoking(() => Mod().ApplyAccountActionAsync(999_999, ModeratorActionType.WarnUser, "x"))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task TwoRacingResolutions_ExactlyOneWins_AndTheCounterMovesOnce()
    {
        int storyId = await SeedStoryAsync(await SeedUserAsync("Author"));
        long report = await SubmitAsAsync(await SeedUserAsync("R1"), ReportedEntityType.Story, storyId);
        await SubmitAsAsync(await SeedUserAsync("R2"), ReportedEntityType.Story, storyId);
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));

        // Lock-serialized contention: the loser blocks on FOR UPDATE, then reads the committed status
        // (testing.md: the Task.WhenAll shape is right where a lock serializes and "one wins" is the claim).
        Task a = Mod().ResolveNoActionAsync(report, "A");
        Task b = Mod().ResolveNoActionAsync(report, "B");
        Task all = Task.WhenAll(a, b);
        try { await all; } catch { /* inspected below */ }

        new[] { a, b }.Count(t => t.IsCompletedSuccessfully).Should().Be(1);
        new[] { a, b }.Single(t => t.IsFaulted).Exception!.InnerException.Should().BeOfType<ModerationValidationException>();
        (await StoryCounterAsync(storyId)).Should().Be(1);
    }

    [Fact]
    public async Task RemovalOfAUserReport_IsRefused_AndTheReportStaysOpen()
    {
        int target = await SeedUserAsync("ReportedUser");
        long report = await SubmitAsAsync(await SeedUserAsync("R1"), ReportedEntityType.User, target);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await FluentActions.Invoking(() => Mod().ResolveWithRemovalAsync(report, "x"))
            .Should().ThrowAsync<ModerationValidationException>().WithMessage("*account action*");

        using IServiceScope scope = Factory.Services.CreateScope();
        (await Db(scope).Reports.SingleAsync(r => r.ReportId == report)).ReportStatusId.Should().Be(ReportStatusEnum.Open);
    }

    // ── §2.4.4: submission dedup and order ───────────────────────────────────────

    [Fact]
    public async Task ADuplicateOpenReport_IsRefused_AndCountedOnce()
    {
        int storyId = await SeedStoryAsync(await SeedUserAsync("Author"));
        int reporter = await SeedUserAsync("R1");
        await SubmitAsAsync(reporter, ReportedEntityType.Story, storyId);

        await FluentActions.Invoking(() => SubmitAsAsync(reporter, ReportedEntityType.Story, storyId))
            .Should().ThrowAsync<ModerationValidationException>().WithMessage("*already reported this*");

        using IServiceScope scope = Factory.Services.CreateScope();
        (await Db(scope).Reports.CountAsync(r => r.ReportedEntityId == storyId)).Should().Be(1);
        (await StoryCounterAsync(storyId)).Should().Be(1, "one account no longer adds +N to the triage sort");
    }

    [Fact]
    public async Task AReporterMayReportAgain_OnceTheirReportIsResolved()
    {
        int storyId = await SeedStoryAsync(await SeedUserAsync("Author"));
        int reporter = await SeedUserAsync("R1");
        long first = await SubmitAsAsync(reporter, ReportedEntityType.Story, storyId);
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await Mod().ResolveNoActionAsync(first, "Fine.");

        long second = await SubmitAsAsync(reporter, ReportedEntityType.Story, storyId);

        second.Should().NotBe(first);
        (await StoryCounterAsync(storyId)).Should().Be(1);
    }

    [Fact]
    public async Task AnonymousReports_AreNotDeduped()
    {
        int storyId = await SeedStoryAsync(await SeedUserAsync("Author"));
        SetActiveUser(FakeActiveUserContext.Anonymous());
        await Submission().SubmitReportAsync(new SubmitReportRequest(ReportedEntityType.Story, storyId, _reasonId, null));
        await Submission().SubmitReportAsync(new SubmitReportRequest(ReportedEntityType.Story, storyId, _reasonId, null));

        using IServiceScope scope = Factory.Services.CreateScope();
        (await Db(scope).Reports.CountAsync(r => r.ReportedEntityId == storyId && r.ReporterUserId == null))
            .Should().Be(2, "a NULL reporter is distinct in the partial unique index");
        (await StoryCounterAsync(storyId)).Should().Be(2);
    }

    [Fact]
    public async Task ADuplicateLandingBetweenTheCheckAndTheInsert_IsRefusedByTheIndex_WithTheSameMessage()
    {
        int storyId = await SeedStoryAsync(await SeedUserAsync("Author"));
        int reporter = await SeedUserAsync("R1");
        SetActiveUser(reporter);

        // The competing open report by the same reporter lands just before our INSERT — after the
        // service's AnyAsync check — so only the partial unique index can refuse it.
        InterleavingCommandInterceptor interceptor = new(ConnectionString, "INSERT INTO reports",
            interleavedSql: $"""
                INSERT INTO reports (reporter_user_id, reported_entity_type, reported_entity_id,
                                     report_reason_id, report_status_id, date_reported)
                VALUES ({reporter}, {(short)ReportedEntityType.Story}, {storyId}, {_reasonId}, 0, now());
                """,
            interceptReaders: true);
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerModerationWriteService svc, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerModerationWriteService>(scope, ConnectionString, interceptor);
        await using (writeDb)
        {
            await FluentActions.Invoking(() => svc.SubmitReportAsync(
                    new SubmitReportRequest(ReportedEntityType.Story, storyId, _reasonId, null)))
                .Should().ThrowAsync<ModerationValidationException>().WithMessage("*already reported this*",
                    "the race is answered like the common case — never a raw 500");
        }
        interceptor.Fired.Should().BeTrue();

        using IServiceScope verify = Factory.Services.CreateScope();
        (await Db(verify).Reports.CountAsync(r => r.ReportedEntityId == storyId)).Should().Be(1);
        (await StoryCounterAsync(storyId)).Should().Be(0,
            "the counter moves only after the row commits (D22 order) — the refused insert never bumped it");
    }

    [Fact]
    public async Task TheUniqueIndex_RefusesTwoOpenReportsByOneReporterOnOneTarget()
    {
        int storyId = await SeedStoryAsync();
        int reporter = await SeedUserAsync("R1");
        await SeedReportRowAsync(ReportedEntityType.Story, storyId, reporter);

        Func<Task> second = () => SeedReportRowAsync(ReportedEntityType.Story, storyId, reporter);
        (await second.Should().ThrowAsync<DbUpdateException>())
            .Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }

    // ── D8: ReportedUserId ───────────────────────────────────────────────────────

    [Fact]
    public async Task ReportedUserId_ResolvesTheAnswerableAccount_ForEveryTargetType()
    {
        int author = await SeedUserAsync("Author");
        int profileOwner = await SeedUserAsync("ProfileOwner");
        int commenter = await SeedUserAsync("Commenter");
        int recommender = await SeedUserAsync("Recommender");
        int sender = await SeedUserAsync("Sender");
        int reporter = await SeedUserAsync("Reporter");
        int storyId = await SeedStoryAsync(author);
        long commentId = await SeedProfileCommentAsync(profileOwner, commenter);
        int blogPostId = await SeedBlogPostAsync(author);
        int recId = await SeedRecommendationAsync(storyId, recommender);
        long messageId = await SeedMessageAsync(sender, reporter);

        var expectations = new (ReportedEntityType Type, long Id, int Expected)[]
        {
            (ReportedEntityType.Story, storyId, author),
            (ReportedEntityType.Comment, commentId, commenter),
            (ReportedEntityType.BlogPost, blogPostId, author),
            (ReportedEntityType.Recommendation, recId, recommender),
            (ReportedEntityType.Message, messageId, sender),
            (ReportedEntityType.User, profileOwner, profileOwner),
        };
        foreach ((ReportedEntityType type, long id, _) in expectations)
            await SubmitAsAsync(reporter, type, id);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Db(scope);
        foreach ((ReportedEntityType type, long id, int expected) in expectations)
            (await db.Reports.SingleAsync(r => r.ReportedEntityType == type && r.ReportedEntityId == id))
                .ReportedUserId.Should().Be(expected, $"the answerable account for a {type} report");
    }

    [Fact]
    public async Task AuthorlessContent_StaysReportable_WithANullReportedUser()
    {
        int storyId = await SeedStoryAsync(authorId: null);

        long report = await SubmitAsAsync(await SeedUserAsync("R1"), ReportedEntityType.Story, storyId);

        using IServiceScope scope = Factory.Services.CreateScope();
        (await Db(scope).Reports.SingleAsync(r => r.ReportId == report)).ReportedUserId.Should().BeNull(
            "submission uses the nullable resolver form and never throws on NULL (D8)");
    }

    [Fact]
    public async Task DeletingTheAnswerableAccount_NullsReportedUser_AndKeepsTheRow()
    {
        int author = await SeedUserAsync("Author");
        int storyId = await SeedStoryAsync(author);
        long report = await SubmitAsAsync(await SeedUserAsync("R1"), ReportedEntityType.Story, storyId);

        await DeleteUserAsync(author);

        using IServiceScope scope = Factory.Services.CreateScope();
        Report row = await Db(scope).Reports.SingleAsync(r => r.ReportId == report);
        row.ReportedUserId.Should().BeNull("ON DELETE SET NULL — reports outlive the accounts they name");
        row.ReportStatusId.Should().Be(ReportStatusEnum.Open,
            "the story survives its author's deletion (SET NULL), so its report is still a live question");
    }

    [Fact]
    public async Task History_ReadsReportedUserId_AndKeepsRowsWhoseTargetIsGone()
    {
        int author = await SeedUserAsync("Author");
        int keptStory = await SeedStoryAsync(author);
        int removedStory = await SeedStoryAsync(author);
        await SubmitAsAsync(await SeedUserAsync("R1"), ReportedEntityType.Story, keptStory);
        long goneReport = await SubmitAsAsync(await SeedUserAsync("R2"), ReportedEntityType.Story, removedStory);
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await Mod().ResolveWithRemovalAsync(goneReport, "Illegal.", hardDelete: true);

        UserModerationHistoryDto history = (await Mod().GetUserModerationHistoryAsync(author))!;

        history.Reports.Should().HaveCount(2, "content reports are part of the person's history (B18)");
        ReportQueueItemDto gone = history.Reports.Single(r => r.ReportId == goneReport);
        gone.TargetLabel.Should().Be("[deleted Story]", "the ledger outlives its target — the row is kept, not dropped");
        gone.TargetUrl.Should().BeNull();
    }

    // ── D7 sub-edge: zombie closure at the source (account deletion) ─────────────

    [Fact]
    public async Task DeletingAUser_ClosesReportsOnTheAccountAndItsProfileComments_ButNotOnSurvivingContent()
    {
        int doomed = await SeedUserAsync("Doomed");
        int commenter = await SeedUserAsync("Commenter");
        int storyId = await SeedStoryAsync(doomed);
        long profileCommentId = await SeedProfileCommentAsync(doomed, commenter);
        long userReport = await SubmitAsAsync(await SeedUserAsync("R1"), ReportedEntityType.User, doomed);
        long commentReport = await SubmitAsAsync(await SeedUserAsync("R2"), ReportedEntityType.Comment, profileCommentId);
        long storyReport = await SubmitAsAsync(await SeedUserAsync("R3"), ReportedEntityType.Story, storyId);

        await DeleteUserAsync(doomed);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Db(scope);
        foreach (long closed in new[] { userReport, commentReport })
        {
            Report row = await db.Reports.SingleAsync(r => r.ReportId == closed);
            row.ReportStatusId.Should().Be(ReportStatusEnum.ResolvedNoAction,
                "no moderation action happened — ResolvedActionTaken would read as a prior sanction");
            row.ModeratorUserId.Should().BeNull();
            row.ActionTaken.Should().StartWith("Closed automatically");
            row.DateResolved.Should().NotBeNull();
        }
        (await db.Reports.SingleAsync(r => r.ReportId == storyReport)).ReportStatusId
            .Should().Be(ReportStatusEnum.Open, "the story survives (SET NULL), so its report stays actionable");
        (await db.Notifications.AnyAsync(n => n.NotificationTypeId == NotificationTypeEnum.ReportResolvedNoAction
            || n.NotificationTypeId == NotificationTypeEnum.ReportResolved)).Should().BeFalse(
            "closure at the source is silent — 81/82 would claim a moderator review that never happened");
    }

    // ── §2.1.3: the account-status transition table and Reinstate ────────────────

    [Fact]
    public async Task Suspend_WithoutAFutureEndDate_IsRefused()
    {
        int target = await SeedUserAsync("Target");
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));

        foreach (DateTime? until in new DateTime?[] { null, DateTime.UtcNow.AddMinutes(-1) })
            await FluentActions.Invoking(() => Mod().ApplyAccountActionToUserAsync(
                    target, _reasonId, ModeratorActionType.SuspendUser, "x", until))
                .Should().ThrowAsync<ModerationValidationException>().WithMessage("*suspension end date in the future*");

        (await LoadUserAsync(target)).AccountStatus.Should().Be(AccountStatusEnum.Active,
            "a null-dated suspension used to be written verbatim — the user signed straight back in");
    }

    [Fact]
    public async Task EveryActionButReinstate_IsRefusedOnABannedUser()
    {
        int target = await SeedUserAsync("Target");
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await Mod().ApplyAccountActionToUserAsync(target, _reasonId, ModeratorActionType.BanUser, "Banned.");

        await FluentActions.Invoking(() => Mod().ApplyAccountActionToUserAsync(
            target, _reasonId, ModeratorActionType.WarnUser, "x")).Should().ThrowAsync<ModerationValidationException>()
            .WithMessage("*lifted only by Reinstate*", "warning a banned user used to silently unban them");
        await FluentActions.Invoking(() => Mod().ApplyAccountActionToUserAsync(
            target, _reasonId, ModeratorActionType.SuspendUser, "x", DateTime.UtcNow.AddDays(3)))
            .Should().ThrowAsync<ModerationValidationException>();
        await FluentActions.Invoking(() => Mod().ApplyAccountActionToUserAsync(
            target, _reasonId, ModeratorActionType.BanUser, "x")).Should().ThrowAsync<ModerationValidationException>()
            .WithMessage("*already banned*");

        (await LoadUserAsync(target)).AccountStatus.Should().Be(AccountStatusEnum.Banned);
    }

    [Fact]
    public async Task Warn_OnALiveSuspension_IsRefused_ButAllowedOnceItHasExpired()
    {
        int target = await SeedUserAsync("Target");
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await Mod().ApplyAccountActionToUserAsync(target, _reasonId, ModeratorActionType.SuspendUser, "x",
            DateTime.UtcNow.AddDays(3));

        await FluentActions.Invoking(() => Mod().ApplyAccountActionToUserAsync(
                target, _reasonId, ModeratorActionType.WarnUser, "x"))
            .Should().ThrowAsync<ModerationValidationException>().WithMessage("*would lift the suspension*");

        await SetUserStatusAsync(target, AccountStatusEnum.Suspended, DateTime.UtcNow.AddDays(-1));
        await Mod().ApplyAccountActionToUserAsync(target, _reasonId, ModeratorActionType.WarnUser, "Expired, now warned.");
        User user = await LoadUserAsync(target);
        user.AccountStatus.Should().Be(AccountStatusEnum.Warned);
        user.SuspendedUntilUtc.Should().BeNull("every non-Suspend status clears the date");
    }

    [Fact]
    public async Task Ban_OnASuspendedUser_ClearsTheSuspensionDate()
    {
        int target = await SeedUserAsync("Target");
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await Mod().ApplyAccountActionToUserAsync(target, _reasonId, ModeratorActionType.SuspendUser, "x",
            DateTime.UtcNow.AddDays(3));
        await Mod().ApplyAccountActionToUserAsync(target, _reasonId, ModeratorActionType.BanUser, "Escalated.");

        User user = await LoadUserAsync(target);
        user.AccountStatus.Should().Be(AccountStatusEnum.Banned);
        user.SuspendedUntilUtc.Should().BeNull("'set only while Suspended' — a stale date used to survive the ban");
    }

    [Fact]
    public async Task Reinstate_ReturnsABannedUserToActive_FilesOneAuditRow_AndTheyCanSignIn()
    {
        int target = await SeedUserAsync("Target");
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await Mod().ApplyAccountActionToUserAsync(target, _reasonId, ModeratorActionType.BanUser, "Banned.");
        int counterBefore = (await LoadUserAsync(target)).ActiveReportCount;

        await Mod().ReinstateUserAsync(target, "  Appeal upheld.  ");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Db(scope);
        User user = await db.Users.SingleAsync(u => u.Id == target);
        user.AccountStatus.Should().Be(AccountStatusEnum.Active);
        user.SuspendedUntilUtc.Should().BeNull();
        user.ActiveReportCount.Should().Be(counterBefore, "the audit row opens and resolves together");

        Report audit = await db.Reports.Where(r => r.ReportedEntityType == ReportedEntityType.User
                && r.ReportedEntityId == target).OrderByDescending(r => r.ReportId).FirstAsync();
        audit.ReportReasonId.Should().Be(1, "the seeded 'Other' reason — the administrative-row precedent");
        audit.ReporterUserId.Should().Be(_modId);
        audit.ModeratorUserId.Should().Be(_modId);
        audit.ReportedUserId.Should().Be(target);
        audit.ReportStatusId.Should().Be(ReportStatusEnum.ResolvedActionTaken);
        audit.ActionTaken.Should().Be("Appeal upheld.");
        audit.Notes.Should().Be("Appeal upheld.");

        SignInManager<User> signIn = scope.ServiceProvider.GetRequiredService<SignInManager<User>>();
        (await signIn.CanSignInAsync(user)).Should().BeTrue("a wrongful ban is reversible in-app (service §2.1.3(b))");
    }

    [Fact]
    public async Task Reinstate_IsRefusedForAnActiveUser_Self_AnUnknownUser_AndANonModerator()
    {
        int target = await SeedUserAsync("Target");
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));

        await FluentActions.Invoking(() => Mod().ReinstateUserAsync(target, "x"))
            .Should().ThrowAsync<ModerationValidationException>().WithMessage("*already active*");
        await FluentActions.Invoking(() => Mod().ReinstateUserAsync(_modId, "x"))
            .Should().ThrowAsync<ModerationValidationException>().WithMessage("*yourself*");
        await FluentActions.Invoking(() => Mod().ReinstateUserAsync(999_999, "x"))
            .Should().ThrowAsync<KeyNotFoundException>();

        SetActiveUser(target);
        await FluentActions.Invoking(() => Mod().ReinstateUserAsync(_modId, "x"))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task TheAccountActionPaths_RefuseReinstateUser()
    {
        int target = await SeedUserAsync("Target");
        long report = await SubmitAsAsync(await SeedUserAsync("R1"), ReportedEntityType.User, target);
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));

        await FluentActions.Invoking(() => Mod().ApplyAccountActionAsync(report, ModeratorActionType.ReinstateUser, "x"))
            .Should().ThrowAsync<ModerationValidationException>().WithMessage("*use Reinstate*");
        await FluentActions.Invoking(() => Mod().ApplyAccountActionToUserAsync(
                target, _reasonId, ModeratorActionType.ReinstateUser, "x"))
            .Should().ThrowAsync<ModerationValidationException>().WithMessage("*use Reinstate*");
    }

    [Fact]
    public async Task AReportDrivenAccountAction_TellsTheMemberReporterTheOutcome()
    {
        int author = await SeedUserAsync("Author");
        int reporter = await SeedUserAsync("R1");
        long report = await SubmitAsAsync(reporter, ReportedEntityType.Story, await SeedStoryAsync(author));

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await Mod().ApplyAccountActionAsync(report, ModeratorActionType.WarnUser, "Warned.");

        using IServiceScope scope = Factory.Services.CreateScope();
        Notification resolved = await Db(scope).Notifications.SingleAsync(n => n.RecipientUserId == reporter
            && n.NotificationTypeId == NotificationTypeEnum.ReportResolved);
        resolved.RelatedEntityId.Should().Be(report, "spec §5.21: reporters always learn the outcome");
        resolved.SourceUserId.Should().BeNull("D5: the moderator is never named");
    }

    // ── D9: moderator-only reads gate in the service ─────────────────────────────

    [Fact]
    public async Task EveryModeratorOnlyRead_RefusesASignedInNonModerator()
    {
        int plainUser = await SeedUserAsync("Plain");
        SetActiveUser(plainUser);
        using IServiceScope scope = Factory.Services.CreateScope();
        IModerationReadService moderation = scope.ServiceProvider.GetRequiredService<IModerationReadService>();
        IExternalVerificationReadService verification = scope.ServiceProvider.GetRequiredService<IExternalVerificationReadService>();
        ISiteDailyStatReadService stats = scope.ServiceProvider.GetRequiredService<ISiteDailyStatReadService>();
        ISpotlightSlotAllocator allocator = scope.ServiceProvider.GetRequiredService<ISpotlightSlotAllocator>();

        Func<Task>[] reads =
        [
            () => moderation.GetReportQueueAsync(),
            () => moderation.GetPendingSubmissionsAsync(),
            () => moderation.GetUserModerationHistoryAsync(plainUser),
            () => verification.GetPendingAccountVerificationsAsync(),
            () => verification.GetPendingLinkVerificationsAsync(),
            () => stats.GetLatestAsync(),
            () => stats.GetSeriesAsync(7),
            () => allocator.GetRemainingMonthlyGrantCapacityAsync(),
        ];
        foreach (Func<Task> read in reads)
            await read.Should().ThrowAsync<UnauthorizedAccessException>(
                "the page's [Authorize] does not protect the circuit — the service is the gate (D9)");

        // Throws rather than returning empty: a mis-registered surface must look broken, not empty.
        // The reporter-facing reason list stays open to any signed-in user.
        (await scope.ServiceProvider.GetRequiredService<IReportSubmissionService>().GetReportReasonsAsync())
            .Should().NotBeEmpty();
    }

    [Fact]
    public async Task AModeratorOnlyRead_RefusesAnAnonymousCaller_AsUnauthenticated()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());
        using IServiceScope scope = Factory.Services.CreateScope();

        await FluentActions.Invoking(() => scope.ServiceProvider.GetRequiredService<IModerationReadService>()
                .GetReportQueueAsync())
            .Should().ThrowAsync<InvalidOperationException>("anonymous → 401, the shared guard's other branch");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static ApplicationDbContext Db(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    private IModerationWriteService Mod()
    {
        IServiceScope scope = Factory.Services.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<IModerationWriteService>();
    }

    private IReportSubmissionService Submission()
    {
        IServiceScope scope = Factory.Services.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<IReportSubmissionService>();
    }

    /// <summary>Files a report through the real submission path as <paramref name="reporterId"/> and
    /// returns its id. Leaves the active user as the reporter.</summary>
    private async Task<long> SubmitAsAsync(int reporterId, ReportedEntityType type, long id)
    {
        SetActiveUser(reporterId);
        await Submission().SubmitReportAsync(new SubmitReportRequest(type, id, _reasonId, null));
        using IServiceScope scope = Factory.Services.CreateScope();
        return await Db(scope).Reports
            .Where(r => r.ReporterUserId == reporterId && r.ReportedEntityType == type && r.ReportedEntityId == id)
            .OrderByDescending(r => r.ReportId).Select(r => r.ReportId).FirstAsync();
    }

    /// <summary>Inserts an Open report row directly (no counter, no checks) — for shapes the submit path
    /// cannot produce, and for exercising the index itself.</summary>
    private async Task<long> SeedReportRowAsync(ReportedEntityType type, long id, int? reporterId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Db(scope);
        Report report = new()
        {
            ReportedEntityType = type, ReportedEntityId = id, ReportReasonId = _reasonId,
            ReporterUserId = reporterId, ReportStatusId = ReportStatusEnum.Open, DateReported = DateTime.UtcNow,
        };
        db.Reports.Add(report);
        await db.SaveChangesAsync();
        return report.ReportId;
    }

    private async Task<int> StoryCounterAsync(int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await Db(scope).Stories.Where(s => s.StoryId == storyId).Select(s => s.ActiveReportCount).SingleAsync();
    }

    private async Task<User> LoadUserAsync(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await Db(scope).Users.AsNoTracking().SingleAsync(u => u.Id == userId);
    }

    private async Task SetUserStatusAsync(int userId, AccountStatusEnum status, DateTime? until)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await Db(scope).Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s
            .SetProperty(u => u.AccountStatus, status)
            .SetProperty(u => u.SuspendedUntilUtc, until));
    }

    private async Task DeleteUserAsync(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<UserDeletionService>().DeleteUserAsync(userId))
            .Should().BeTrue();
    }

    /// <summary>FK parents: both users (profile owner, commenter).</summary>
    private async Task<long> SeedProfileCommentAsync(int profileUserId, int commenterId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Db(scope);
        UserProfileComment comment = new()
        {
            ProfileUserId = profileUserId, UserId = commenterId, CommentText = "a wall comment", DatePosted = DateTime.UtcNow,
        };
        db.UserProfileComments.Add(comment);
        await db.SaveChangesAsync();
        return comment.CommentId;
    }

    /// <summary>FK parent: the author. Published and E-rated, so a reporter can see it.</summary>
    private async Task<int> SeedBlogPostAsync(int authorId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Db(scope);
        ProfileBlogPost post = new()
        {
            AuthorId = authorId, Title = "A post", Content = "<p>content</p>", IsPublished = true,
            DateCreated = DateTime.UtcNow, LastUpdatedDate = DateTime.UtcNow, Rating = Rating.E,
        };
        db.ProfileBlogPosts.Add(post);
        await db.SaveChangesAsync();
        return post.BlogPostId;
    }

    /// <summary>FK parents: the story and the recommender. Approved, so a reporter can see it.</summary>
    private async Task<int> SeedRecommendationAsync(int storyId, int recommenderId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Db(scope);
        Recommendation rec = new()
        {
            StoryId = storyId, RecommenderId = recommenderId,
            StatusId = (short)RecommendationStatusEnum.Approved, DatePosted = DateTime.UtcNow,
            RecommendationDetail = new RecommendationDetail { Text = "<p>read this</p>" },
        };
        db.Recommendations.Add(rec);
        await db.SaveChangesAsync();
        return rec.RecommendationId;
    }

    /// <summary>FK parents: a conversation (parentless), both participants, then the message.</summary>
    private async Task<long> SeedMessageAsync(int senderId, int recipientId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Db(scope);
        Conversation conversation = new() { Subject = "Hi", DateCreated = DateTime.UtcNow };
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync();
        db.ConversationParticipants.AddRange(
            new ConversationParticipant { ConversationId = conversation.ConversationId, UserId = senderId },
            new ConversationParticipant { ConversationId = conversation.ConversationId, UserId = recipientId });
        PrivateMessage message = new()
        {
            ConversationId = conversation.ConversationId, SenderUserId = senderId,
            MessageText = "<p>hello</p>", DateSent = DateTime.UtcNow,
        };
        db.PrivateMessages.Add(message);
        await db.SaveChangesAsync();
        return message.MessageId;
    }
}
