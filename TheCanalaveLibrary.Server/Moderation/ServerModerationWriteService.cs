using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side write implementation of <see cref="IModerationWriteService"/>, and of the member-facing
/// <see cref="IReportSubmissionService"/> (owner ruling D9's split — one concrete class, registered once,
/// all three interfaces forwarded to it). Inherits the read path via primary-constructor chaining
/// (CQRS-lite with write-inherits-read). Rules: <c>layer2-services.md</c> §"Moderation Services".
///
/// <para><b>Target-type allow-set.</b> Only Story, User, Comment, BlogPost, Recommendation, and
/// Message may be reported. Any other type throws <see cref="ModerationValidationException"/> (400).
/// Messages have no <c>ActiveReportCount</c> column — <see cref="AdjustActiveReportCountAsync"/>
/// is a no-op for that type (<c>content-safety.md</c> §"Report Targets and ActiveReportCount").</para>
///
/// <para><b>IModeratableContent.</b> Story, Comment, BlogPost, and Recommendation implement
/// <see cref="IModeratableContent"/>; their soft-remove and hard-delete operations are handled
/// through shared interface code after a single per-type load. User and Message stay explicitly
/// special-cased: a User report is resolved with an account action (removal refuses it); a Message
/// goes straight to hard-delete with no takedown columns.</para>
///
/// <para><b>Content-rating filter.</b> The write context (<c>writeDb</c>) is never filtered — every
/// action here already acts on ground truth regardless of rating, and always has. The read side
/// (<see cref="ServerModerationReadService"/>) now matches: review/entity-load reads bypass
/// <c>IsTakenDown</c> and ContentRating/GroupAudience alike, since moderation review is a work
/// surface exempt from the reviewer's personal ShowMatureContent setting (settled 2026-07-18,
/// supersedes the 2026-06-26 "mirrors browsing" framing — see <c>content-safety.md</c>
/// §"Moderator review surfaces are work surfaces").</para>
///
/// <para><b>Resolve paths lock, guard, then transition (service §2.1.2).</b> Each runs in one
/// execution-strategy transaction that opens by locking the report row (<c>FOR UPDATE</c>) and refusing
/// one that is no longer Open/UnderReview, so a second moderator blocks, then reads the committed
/// status and is refused — nothing decrements twice. Removal also closes the target's sibling reports
/// (owner ruling D7).</para>
///
/// <para><b>Notifications are best-effort.</b> Every <c>NotifyXxx</c> call happens <em>after</em>
/// the primary write commits — on the resolve paths, after the execution strategy returns, never
/// inside the retried delegate — each inside its own <c>try/catch</c> that logs and swallows, so a
/// notification failure never rolls back a moderation action and never drops the next one.</para>
///
/// <para><b>Notifications never name the moderator (owner rulings D4/D5).</b> Every outcome
/// notification this service sends (70–82) is null-sourced — the <c>Report</c> row keeps the real
/// <c>ModeratorUserId</c> as the internal ledger. The report outcomes (70/80/81/82) carry the report
/// id so two outcomes for one recipient never collapse. Guardrail: the moderator-initiated account
/// action (<see cref="ApplyAccountActionToUserAsync"/>) must never send 80 or 81 — a null source
/// no longer drop-selfs, so it would mail the moderator receipts for their own action. The same
/// reasoning binds every other call site here: the acting moderator is never a recipient of a
/// notification about their own act (a report they filed and resolved, their own removed content,
/// their own story, a report-driven action on themselves) — each site skips that recipient
/// explicitly, doing the job drop-self did before D5 (layer2-services.md §"Notification
/// Generation").</para>
/// </summary>
public class ServerModerationWriteService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser,
    INotificationWriteService notifications,
    IWriteRateLimitService rateLimit,
    UserManager<User> userManager,
    ILogger<ServerModerationWriteService> logger)
    : ServerModerationReadService(readDbFactory, activeUser), IModerationWriteService, IReportSubmissionService
{
    // ── Allowed reportable entity types ──────────────────────────────────────────

    private static readonly HashSet<ReportedEntityType> AllowedReportTargets =
    [
        ReportedEntityType.Story,
        ReportedEntityType.User,
        ReportedEntityType.Comment,
        ReportedEntityType.BlogPost,
        ReportedEntityType.Recommendation,
        ReportedEntityType.Message,
    ];

    /// <summary>The partial unique index that enforces one open report per reporter per target —
    /// matched by name in <see cref="SubmitReportAsync"/>'s race catch.</summary>
    internal const string OpenReporterTargetIndex = "ix_reports_open_reporter_target";

    /// <summary>The seeded "Other" report reason (<c>ModerationConfigurations</c> HasData) — the reason
    /// an administrative row carries when no moderator-chosen category applies (Reinstate).</summary>
    private const short OtherReportReasonId = 1;

    /// <summary><c>Report.ActionTaken</c>'s column cap.</summary>
    private const int ActionTakenMaxLength = 1024;

    private const string DuplicateReport =
        "You've already reported this — a moderator will review your open report.";

    private const string AlreadyResolved = "This report has already been resolved.";

    private const string BannedLeavableOnlyByReinstate =
        "This account is banned. A ban is lifted only by Reinstate — reinstate the account first if a " +
        "lighter action is what it should carry.";

    // ── Report submission (Feature 46 — IReportSubmissionService) ─────────────────

    public async Task SubmitReportAsync(SubmitReportRequest request)
    {
        if (!AllowedReportTargets.Contains(request.EntityType))
            throw new ModerationValidationException([$"Entity type '{request.EntityType}' cannot be reported."]);

        int? reporterId = ActiveUser.UserId;
        // Reports may be anonymous (nullable reporter) — throttle only the authenticated axis;
        // report-form UI is auth-gated, so this covers every real path (security.md).
        if (reporterId is int throttleUserId)
            rateLimit.EnsureAllowed(WriteActionKind.Report, throttleUserId);

        // Kind (g), settled 2026-07-26: the target must EXIST always, and must be VISIBLE to the
        // reporter EXCEPT when the only thing hiding it is a takedown. Rationale: this method had no
        // existence check at all, so the queue could be flooded with reports against ids that never
        // existed while AdjustActiveReportCountAsync bumped ActiveReportCount on drafts. The takedown
        // exemption keeps a good-faith report legitimate when content is removed between the moment a
        // user opens the report form and the moment they submit it.
        await RequireReportableTargetAsync(request.EntityType, request.EntityId);

        // One open report per reporter per target (service §2.4.4(c)): without it one account adds +N
        // to the triage sort, and D7's "notify every sibling reporter" could address one user twice.
        // The partial unique index is the backstop; this check gives the common case a clean message.
        // Anonymous reports are not deduped (a NULL reporter is distinct in the index).
        if (reporterId is int dedupReporterId && await writeDb.Reports.AnyAsync(r =>
                r.ReporterUserId == dedupReporterId
                && r.ReportedEntityType == request.EntityType
                && r.ReportedEntityId == request.EntityId
                && (r.ReportStatusId == ReportStatusEnum.Open || r.ReportStatusId == ReportStatusEnum.UnderReview)))
            throw new ModerationValidationException([DuplicateReport]);

        var report = new Report
        {
            ReportedEntityType = request.EntityType,
            ReportedEntityId = request.EntityId,
            ReportReasonId = request.ReasonId,
            Notes = request.Notes,
            ReporterUserId = reporterId,
            ReportStatusId = ReportStatusEnum.Open,
            DateReported = DateTime.UtcNow,
            // D8: who was answerable when the report was filed — a snapshot, never re-resolved. The
            // nullable form never throws: anonymous or deleted-author content stays reportable.
            ReportedUserId = await ResolveAnswerableUserIdAsync(request.EntityType, request.EntityId),
        };

        // Primary write first, counter second (service §2.4.4(b), D22): the old order bumped the counter
        // in its own committed statement before the row's save, so a failure between left a +1 with no
        // row. Now a failure between leaves a missing +1 — transient drift the COUNT(*) recompute heals.
        writeDb.Reports.Add(report);
        try
        {
            await writeDb.SaveChangesAsync();
        }
        // The first catch of a named unique-index violation in this codebase: the AnyAsync above and a
        // concurrent submit by the same reporter can both pass, and the index refuses the second insert.
        // Matching the constraint name keeps any other 23505 a genuine fault. The rejected row is
        // detached so a circuit-scoped context does not retry it on its next save.
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: OpenReporterTargetIndex,
        })
        {
            writeDb.Entry(report).State = EntityState.Detached;
            throw new ModerationValidationException([DuplicateReport]);
        }

        await AdjustActiveReportCountAsync(request.EntityType, request.EntityId, +1);

        if (reporterId is int reporter)
        {
            // Null-sourced, carrying the report id (owner ruling D4): at submission no moderator exists, and
            // the old call passed the reporter as their own source, so drop-self deleted every receipt.
            await NotifyBestEffortAsync(() => notifications.NotifyReportReceivedAsync(reporter, report.ReportId),
                "ReportReceived", report.ReportId);
        }
    }

    // ── Moderator queue actions (Feature 47) ─────────────────────────────────────

    public async Task ClaimReportAsync(long reportId)
    {
        int modId = ActiveUser.RequireModerator();
        await writeDb.Reports
            .Where(r => r.ReportId == reportId && r.ReportStatusId == ReportStatusEnum.Open)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.ReportStatusId, ReportStatusEnum.UnderReview)
                .SetProperty(r => r.ModeratorUserId, modId));
    }

    public async Task ResolveNoActionAsync(long reportId, string? actionNotes)
    {
        int modId = ActiveUser.RequireModerator();

        // No sibling closing (owner ruling D7): the target is unchanged and every other report on it is
        // still genuinely actionable — one moderator's "no" is a ruling on one complaint.
        int? reporterUserId = await InResolveTransactionAsync(async () =>
        {
            Report report = await LockResolvableReportAsync(reportId);

            report.ReportStatusId = ReportStatusEnum.ResolvedNoAction;
            report.ModeratorUserId = modId;
            report.ActionTaken = actionNotes;
            report.DateResolved = DateTime.UtcNow;
            await writeDb.SaveChangesAsync();

            await AdjustActiveReportCountAsync(report.ReportedEntityType, report.ReportedEntityId, -1);
            return report.ReporterUserId;
        });

        // A moderator who filed this report through the ordinary Report button and now resolves it
        // gets no receipt for their own act (the D4 guardrail's general rule).
        if (reporterUserId is int reporter && reporter != modId)
            await NotifyBestEffortAsync(() => notifications.NotifyReportResolvedNoActionAsync(reporter, reportId),
                "ReportResolvedNoAction", reportId);
    }

    public async Task ResolveWithRemovalAsync(long reportId, string removalReason, bool hardDelete = false)
    {
        int modId = ActiveUser.RequireModerator();

        RemovalOutcome outcome = await InResolveTransactionAsync(async () =>
        {
            // (1) Lock and guard the primary report.
            Report report = await LockResolvableReportAsync(reportId);
            ReportedEntityType type = report.ReportedEntityType;
            long entityId = report.ReportedEntityId;

            // ApplyRemovalAsync has no User branch, and D7 forbids bulk-closing a user's reports ("neither
            // account-action path bulk-closes") — a User report is resolved with an account action. This
            // is also the shape of D47(a)'s floor (removal unreachable for a target it cannot act on).
            if (type == ReportedEntityType.User)
                throw new ModerationValidationException(
                    ["User reports are resolved with an account action, not a removal."]);

            // (2) The removal itself (tracked; committed by the save below).
            int? contentAuthorId = hardDelete
                ? await ApplyHardDeleteAsync(type, entityId)
                : await ApplyRemovalAsync(type, entityId, removalReason);

            // (3) The primary report, tracked as before.
            DateTime now = DateTime.UtcNow;
            report.ReportStatusId = ReportStatusEnum.ResolvedActionTaken;
            report.ModeratorUserId = modId;
            report.ActionTaken = removalReason;
            report.DateResolved = now;

            // (4) Sibling closing (owner ruling D7). Keyed on the (type, id) PAIR — never the id alone
            // (story 5 and comment 5 are different targets) and never the target's author. Rows are
            // locked first so the closing set is exactly the set notified below; a sibling claimed
            // UnderReview by another moderator closes too (a claim is triage bookkeeping, not a lock).
            // The primary is excluded, so the tracked row and the set-based update never collide.
            short typeValue = (short)type;
            short open = (short)ReportStatusEnum.Open, underReview = (short)ReportStatusEnum.UnderReview;
            List<Report> siblings = await writeDb.Reports
                .FromSql($"""
                    SELECT * FROM reports
                    WHERE reported_entity_type = {typeValue} AND reported_entity_id = {entityId}
                      AND report_status_id IN ({open}, {underReview}) AND report_id <> {reportId}
                    FOR UPDATE
                    """)
                .AsNoTracking()
                .ToListAsync();

            List<long> siblingIds = [..siblings.Select(s => s.ReportId)];
            string siblingNote = Truncate($"Closed with report #{reportId}: {removalReason}", ActionTakenMaxLength);
            int closed = siblingIds.Count == 0 ? 0 : await writeDb.Reports
                .Where(r => siblingIds.Contains(r.ReportId)
                            && (r.ReportStatusId == ReportStatusEnum.Open
                                || r.ReportStatusId == ReportStatusEnum.UnderReview))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.ReportStatusId, ReportStatusEnum.ResolvedActionTaken)
                    .SetProperty(r => r.ModeratorUserId, modId)
                    .SetProperty(r => r.DateResolved, now)
                    .SetProperty(r => r.ActionTaken, siblingNote));

            // (5) The counter moves by the rows actually transitioned — never zeroed, so a report filed
            // concurrently keeps its own +1. Vacuous on hard delete (the row dies in the save below);
            // a no-op for Message, which still gets the sibling half.
            await AdjustActiveReportCountAsync(type, entityId, -(1 + closed));

            // (6) Save the removal and the primary report; the caller commits.
            await writeDb.SaveChangesAsync();

            return new RemovalOutcome(
                report.ReporterUserId,
                contentAuthorId,
                [..siblings
                    .Where(s => s.ReporterUserId is not null)
                    .GroupBy(s => s.ReporterUserId!.Value)
                    .Select(g => (g.First().ReportId, g.Key))]);
        });

        // Each notification in its own best-effort try, so one failure cannot drop the rest. Never the
        // acting moderator (the D4 guardrail's general rule): not as the reporter of a report they then
        // resolved, nor as the author of content they removed themselves.
        if (outcome.ReporterUserId is int reporter && reporter != modId)
            await NotifyBestEffortAsync(() => notifications.NotifyReportResolvedAsync(reporter, reportId),
                "ReportResolved", reportId);

        // D7: "reporters always learn the outcome" (spec §5.21) — every distinct sibling reporter, each
        // with their OWN report id, so D4's dedup never collapses two reporters' rows (and the unique
        // index makes it exactly-once per reporter).
        foreach ((long siblingReportId, int siblingReporter) in outcome.SiblingReporters)
        {
            if (siblingReporter == modId || siblingReporter == outcome.ReporterUserId) continue;
            await NotifyBestEffortAsync(() => notifications.NotifyReportResolvedAsync(siblingReporter, siblingReportId),
                "ReportResolved (sibling)", siblingReportId);
        }

        if (outcome.ContentAuthorId is int author && author != modId)
            await NotifyBestEffortAsync(() => notifications.NotifyContentRemovedAsync(author, reportId),
                "ContentRemoved", reportId);
    }

    public async Task ApplyAccountActionAsync(long reportId, ModeratorActionType action,
        string reason, DateTime? suspendedUntilUtc = null)
    {
        int modId = ActiveUser.RequireModerator();
        AccountStatusEnum newStatus = ToAccountStatus(action);

        // No sibling closing (owner ruling D7): the account changes, the reported artifact does not, and
        // every other report still asks a live question.
        (int targetUserId, int? reporterUserId, bool standingBan) = await InResolveTransactionAsync(async () =>
        {
            Report report = await LockResolvableReportAsync(reportId);

            // The user acted on is RESOLVED from the report, not assumed to be its target: warning over
            // a reported story means warning that story's author — see layer2-services.md §"Account
            // actions — target resolution and the report-as-audit-record rule".
            int targetId = await ResolveActionTargetUserIdAsync(report.ReportedEntityType, report.ReportedEntityId);
            User targetUser = await writeDb.Users.SingleOrDefaultAsync(u => u.Id == targetId)
                ?? throw new ModerationValidationException([NoAccountToActOn]);

            // A standing ban answers this report (derived — roadmap.md decision row 20, refined by the
            // WU-ModerationIntegrity review fixes): the report resolves against the ban already in place,
            // with no status write, no stamp bump and no second 74. Refusing it, as the moderator-
            // initiated path does, would leave every other report about a banned account closable only
            // as "no action" — Warn/Suspend are refused on Banned and a User report cannot be removed.
            bool alreadyBanned = action == ModeratorActionType.BanUser
                                 && targetUser.AccountStatus == AccountStatusEnum.Banned;

            DateTime now = DateTime.UtcNow;
            if (!alreadyBanned)
                EnsureLegalAccountTransition(targetUser, action, suspendedUntilUtc, now);

            report.ReportStatusId = ReportStatusEnum.ResolvedActionTaken;
            report.ModeratorUserId = modId;
            report.ActionTaken = reason;
            report.DateResolved = now;
            if (!alreadyBanned)
                ApplyStatus(targetUser, newStatus, suspendedUntilUtc);
            await writeDb.SaveChangesAsync();

            // UserManager shares the scoped ApplicationDbContext, so the stamp change rides this
            // transaction. Kill live sessions for Suspend/Ban, not Warn (WU38a) — and not for a standing
            // ban, whose sessions were ended when it was imposed.
            if (!alreadyBanned)
                await BumpSecurityStampIfEjectingAsync(targetUser, newStatus);

            // This IS a resolve path, so it decrements like its two siblings do. It runs after the stamp
            // bump on purpose: UserManager writes every column of the tracked user, so a User-target
            // decrement made before it would be overwritten with the loaded value.
            await AdjustActiveReportCountAsync(report.ReportedEntityType, report.ReportedEntityId, -1);

            return (targetId, report.ReporterUserId, alreadyBanned);
        });

        if (!standingBan)
            await NotifyAccountActionAsync(targetUserId, action, modId);

        // Spec §5.21 "reporters always learn the outcome": the member who filed the report hears it was
        // resolved with action taken (81, report id) — never the acting moderator (D4 guardrail).
        if (reporterUserId is int reporter && reporter != modId)
            await NotifyBestEffortAsync(() => notifications.NotifyReportResolvedAsync(reporter, reportId),
                "ReportResolved", reportId);
    }

    public async Task ApplyAccountActionToUserAsync(int targetUserId, short reasonId,
        ModeratorActionType action, string reason, DateTime? suspendedUntilUtc = null)
    {
        int modId = ActiveUser.RequireModerator();
        AccountStatusEnum newStatus = ToAccountStatus(action);

        if (targetUserId == modId)
            throw new ModerationValidationException(["You can't apply an account action to yourself."]);

        User targetUser = await writeDb.Users.SingleOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new KeyNotFoundException($"User {targetUserId} was not found.");

        if (!await writeDb.ReportReasons.AnyAsync(rr => rr.ReportReasonId == reasonId))
            throw new ModerationValidationException(["Choose a reason for this action."]);

        DateTime now = DateTime.UtcNow;
        EnsureLegalAccountTransition(targetUser, action, suspendedUntilUtc, now);

        // The Report row IS the audit record — there is no separate moderation-action table. A
        // moderator acting without a member report files one, and ReporterUserId == ModeratorUserId
        // is what marks it moderator-initiated.
        writeDb.Reports.Add(new Report
        {
            ReportedEntityType = ReportedEntityType.User,
            ReportedEntityId = targetUserId,
            ReportedUserId = targetUserId,
            ReportReasonId = reasonId,
            Notes = reason,
            ReporterUserId = modId,
            ReportStatusId = ReportStatusEnum.ResolvedActionTaken,
            ModeratorUserId = modId,
            ActionTaken = reason,
            DateReported = now,
            DateResolved = now,
        });

        // No AdjustActiveReportCountAsync call: the row opens and resolves in one step, so the
        // +1/-1 pair every other path makes would cancel out.
        ApplyStatus(targetUser, newStatus, suspendedUntilUtc);
        await writeDb.SaveChangesAsync();
        await BumpSecurityStampIfEjectingAsync(targetUser, newStatus);

        // Guardrail (owner ruling D4): this report is moderator-filed (ReporterUserId == modId), so it
        // must NOT send ReportReceived (80) or ReportResolved (81). Those are null-sourced now, so
        // drop-self no longer protects this path — wiring them here would mail the moderator receipts
        // for their own action. Only the target's account notification (72/73/74) is sent.
        await NotifyAccountActionAsync(targetUser.Id, action, modId);
    }

    public async Task ReinstateUserAsync(int targetUserId, string reason)
    {
        int modId = ActiveUser.RequireModerator();

        if (targetUserId == modId)
            throw new ModerationValidationException(["You can't apply an account action to yourself."]);

        User targetUser = await writeDb.Users.SingleOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new KeyNotFoundException($"User {targetUserId} was not found.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new ModerationValidationException(["A reason is required."]);
        string trimmed = reason.Trim();
        // Report.ActionTaken's existing column cap — refused as a 400 rather than a 500. Picks no new number.
        if (trimmed.Length > ActionTakenMaxLength)
            throw new ModerationValidationException(["That reason is too long."]);

        if (targetUser.AccountStatus == AccountStatusEnum.Active)
            throw new ModerationValidationException(["This account is already active."]);

        // Service §2.1.3(b): the only writer of Active, and the only way out of Banned. The audit row is
        // the moderator-initiated shape (layer2-services.md §"Account actions"), reason "Other" — the
        // administrative-row precedent — and opened and resolved together, so ActiveReportCount is
        // untouched. No stamp bump (nothing to eject) and no notification (no type exists — tracker F14).
        DateTime now = DateTime.UtcNow;
        ApplyStatus(targetUser, AccountStatusEnum.Active, suspendedUntilUtc: null);
        writeDb.Reports.Add(new Report
        {
            ReportedEntityType = ReportedEntityType.User,
            ReportedEntityId = targetUserId,
            ReportedUserId = targetUserId,
            ReportReasonId = OtherReportReasonId,
            Notes = trimmed,
            ReporterUserId = modId,
            ReportStatusId = ReportStatusEnum.ResolvedActionTaken,
            ModeratorUserId = modId,
            ActionTaken = trimmed,
            DateReported = now,
            DateResolved = now,
        });

        await writeDb.SaveChangesAsync();
    }

    // ── Submission approval (Feature 48) ─────────────────────────────────────────

    private const string SubmissionAlreadyHandled = "This submission was already handled.";

    // A taken-down story's status is frozen until the takedown is reversed (layer2-services.md
    // §"Story Lifecycle") — the moderator-side twin of TransitionStatusAsync's refusal.
    private const string SubmissionTakenDown =
        "This story is under a moderator takedown, so it can't be approved or rejected unless the takedown is reversed.";

    public async Task ApproveStoryAsync(int storyId)
    {
        // The moderator is not recorded on the story, and the outcome notification is null-sourced
        // (D5); the id only keeps a moderator approving their own story from notifying themselves.
        int modId = ActiveUser.RequireModerator();

        var story = await writeDb.Stories
            .Where(s => s.StoryId == storyId)
            .Select(s => new
            {
                s.StoryStatusId,
                s.AuthorId,
                s.IsTakenDown,
                PostApprovalStatus = s.StoryDetail.PostApprovalStatus,
                AuthorStatus = s.Author != null ? (AccountStatusEnum?)s.Author.AccountStatus : null,
                AuthorSuspendedUntilUtc = s.Author != null ? s.Author.SuspendedUntilUtc : null,
            })
            .SingleOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Story {storyId} was not found.");

        // D1 guards, all user-facing (400) rather than the InvalidOperationException → 401 they
        // replaced. Order: already handled → taken down → unpublishable target → non-live author.
        if (story.StoryStatusId != StoryStatusEnum.PendingApproval)
            throw new ModerationValidationException([SubmissionAlreadyHandled]);
        if (story.IsTakenDown)
            throw new ModerationValidationException([SubmissionTakenDown]);

        // Closes the approve-into-Draft hole: PostApprovalStatus stays editable while queued, so it
        // is re-validated here, not only at submit.
        StoryStatusEnum approvedStatus = story.PostApprovalStatus;
        if (!StoryLifecycle.IsEntryStatus(approvedStatus))
            throw new ModerationValidationException(
            [
                "This submission's \"Status when published\" isn't one a story can be published as " +
                "(In Progress, Complete or Open Beta). Reject it so the author can fix it."
            ]);

        // Live-author guard (D1 sub-edge, owner-stated). Null AuthorId = deleted (D13 hard delete
        // leaves the FK SetNull). A null-dated suspension counts as live-suspended — deliberately
        // stricter than CanalaveSignInManager (it fails closed). WU-ModerationIntegrity's transition
        // table made a null-dated suspension unwritable, so that arm only matters for an older row.
        int authorId = story.AuthorId ?? throw new ModerationValidationException(
            ["This story's author has deleted their account, so it can't be approved — reject it instead."]);
        bool suspendedNow = story.AuthorStatus == AccountStatusEnum.Suspended
            && (story.AuthorSuspendedUntilUtc is null || story.AuthorSuspendedUntilUtc > DateTime.UtcNow);
        if (story.AuthorStatus == AccountStatusEnum.Banned || suspendedNow)
            throw new ModerationValidationException(
                ["This story's author is banned or suspended, so it can't be approved — reject it instead."]);

        // Status flip + the author's monotonic trust record commit together: there is no recompute
        // to heal a split (layer2-services.md §"Records of a decision are not counters"). The flip is
        // conditional on PendingApproval, so a second moderator or an author withdraw in between
        // affects 0 rows → nothing is incremented.
        DateTime now = DateTime.UtcNow;
        var strategy = writeDb.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await writeDb.Database.BeginTransactionAsync();

            int affected = await writeDb.Stories
                .Where(s => s.StoryId == storyId && s.StoryStatusId == StoryStatusEnum.PendingApproval
                            && !s.IsTakenDown)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(s => s.StoryStatusId, approvedStatus)
                    // D2: first publication only — a previously published story keeps its date.
                    .SetProperty(s => s.PublishedDate, s => s.PublishedDate ?? now));
            if (affected == 0)
                throw new ModerationValidationException([SubmissionAlreadyHandled]);

            await writeDb.Users
                .Where(u => u.Id == authorId)
                .ExecuteUpdateAsync(u => u.SetProperty(
                    x => x.ApprovedStorySubmissions, x => x.ApprovedStorySubmissions + 1));

            await tx.CommitAsync();
        });

        if (authorId != modId) // no receipt for the moderator's own act (D4 guardrail)
            await NotifyBestEffortAsync(() => notifications.NotifyStoryApprovedAsync(authorId, storyId),
                "StoryApproved", storyId);
    }

    public async Task RejectStoryAsync(int storyId, string reason)
    {
        int modId = ActiveUser.RequireModerator(); // see ApproveStoryAsync

        var story = await writeDb.Stories
            .Where(s => s.StoryId == storyId)
            .Select(s => new { s.StoryStatusId, s.AuthorId, s.IsTakenDown })
            .SingleOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Story {storyId} was not found.");

        if (story.StoryStatusId != StoryStatusEnum.PendingApproval)
            throw new ModerationValidationException([SubmissionAlreadyHandled]);
        // Rejecting a taken-down row would overwrite the takedown's own TakedownReason/TakedownDate
        // and leave a Rejected story under the takedown — the overlap D1 rules out. The queue never
        // lists such a row (GetPendingSubmissionsAsync keeps the IsTakenDown filter on).
        if (story.IsTakenDown)
            throw new ModerationValidationException([SubmissionTakenDown]);

        // Rejected is reachable only from PendingApproval (D1) — enforced by the conditional update
        // itself, so a race with an approve, a withdraw or a takedown can't reject a row it no longer
        // applies to. No live-author guard here: a moderator must always be able to clear the queue.
        DateTime now = DateTime.UtcNow;
        int affected = await writeDb.Stories
            .Where(s => s.StoryId == storyId && s.StoryStatusId == StoryStatusEnum.PendingApproval
                        && !s.IsTakenDown)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.StoryStatusId, StoryStatusEnum.Rejected)
                .SetProperty(s => s.TakedownReason, reason)
                .SetProperty(s => s.TakedownDate, now));
        if (affected == 0)
            throw new ModerationValidationException([SubmissionAlreadyHandled]);

        if (story.AuthorId is int author && author != modId) // never the acting moderator (D4 guardrail)
            await NotifyBestEffortAsync(() => notifications.NotifyStoryRejectedAsync(author, storyId),
                "StoryRejected", storyId);
    }

    // ── Story-approval trust (WU-StoryLifecycle, D1) ──────────────────────────────

    public async Task SetCanAutoApproveAsync(int targetUserId, bool canAutoApprove, short reasonId, string reason)
    {
        int modId = ActiveUser.RequireModerator();

        if (targetUserId == modId)
            throw new ModerationValidationException(["You can't change your own auto-approve standing."]);

        User targetUser = await writeDb.Users.SingleOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new KeyNotFoundException($"User {targetUserId} was not found.");

        if (!await writeDb.ReportReasons.AnyAsync(rr => rr.ReportReasonId == reasonId))
            throw new ModerationValidationException(["Choose a reason for this action."]);

        if (string.IsNullOrWhiteSpace(reason))
            throw new ModerationValidationException(["A reason is required."]);

        // Unchanged value → no-op, no audit row (nothing happened to record).
        if (targetUser.CanAutoApprove == canAutoApprove) return;

        // Report.ActionTaken's existing column cap (1024) — refused as a 400 rather than letting
        // the insert fail as a 500. Picks no new number.
        string actionTaken = $"Auto-approve {(canAutoApprove ? "restored" : "revoked")}: {reason.Trim()}";
        if (actionTaken.Length > ActionTakenMaxLength)
            throw new ModerationValidationException(["That reason is too long."]);

        targetUser.CanAutoApprove = canAutoApprove;

        // The Report row IS the audit record (layer2-services.md §"Account actions") — same
        // moderator-initiated shape as ApplyAccountActionToUserAsync; opened and resolved together,
        // so ActiveReportCount is untouched. No notification (owner silent).
        DateTime now = DateTime.UtcNow;
        writeDb.Reports.Add(new Report
        {
            ReportedEntityType = ReportedEntityType.User,
            ReportedEntityId = targetUserId,
            ReportedUserId = targetUserId,
            ReportReasonId = reasonId,
            Notes = reason,
            ReporterUserId = modId,
            ReportStatusId = ReportStatusEnum.ResolvedActionTaken,
            ModeratorUserId = modId,
            ActionTaken = actionTaken,
            DateReported = now,
            DateResolved = now,
        });

        await writeDb.SaveChangesAsync();
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private const string NoAccountToActOn =
        "There's no account to act on for this report — the content is anonymous, or its " +
        "author's account has been deleted. Remove the content instead.";

    /// <summary>What a removal's transaction hands to the post-commit notifications.</summary>
    private sealed record RemovalOutcome(
        int? ReporterUserId,
        int? ContentAuthorId,
        List<(long ReportId, int ReporterUserId)> SiblingReporters);

    /// <summary>
    /// Runs one resolve path in a single transaction under the execution strategy (the Spotlight
    /// template — a bare <c>BeginTransactionAsync</c> throws under <c>EnableRetryOnFailure</c>). The
    /// tracker is cleared first because a transient-failure retry re-runs the whole delegate. Callers
    /// notify only after this returns, never inside the delegate, so a retry cannot double-notify.
    /// </summary>
    private async Task<T> InResolveTransactionAsync<T>(Func<Task<T>> body)
    {
        var strategy = writeDb.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            writeDb.ChangeTracker.Clear();
            await using var tx = await writeDb.Database.BeginTransactionAsync();
            T result = await body();
            await tx.CommitAsync();
            return result;
        });
    }

    /// <summary>
    /// Service §2.1.2's guard: locks the report row (<c>FOR UPDATE</c>, inside the caller's transaction)
    /// and returns it tracked. Missing → <see cref="KeyNotFoundException"/> (404); not Open/UnderReview →
    /// <see cref="ModerationValidationException"/> (400). The lock serializes a second moderator, who
    /// blocks, then reads the committed status and is refused — so the counter moves only on the actual
    /// transition, with no concurrency token (D30 is pending). Materialized with <c>ToListAsync</c>, never
    /// composed further: EF would wrap the locking query as a subquery.
    /// </summary>
    private async Task<Report> LockResolvableReportAsync(long reportId)
    {
        List<Report> locked = await writeDb.Reports
            .FromSql($"SELECT * FROM reports WHERE report_id = {reportId} FOR UPDATE")
            .ToListAsync();

        Report report = locked.SingleOrDefault()
            ?? throw new KeyNotFoundException($"Report {reportId} was not found.");

        if (report.ReportStatusId is not (ReportStatusEnum.Open or ReportStatusEnum.UnderReview))
            throw new ModerationValidationException([AlreadyResolved]);

        return report;
    }

    /// <summary>
    /// Maps the three account actions onto their resulting <see cref="AccountStatusEnum"/>. Called
    /// at the top of both public entry points so an invalid action throws before anything mutates.
    /// Reinstate is its own method (<see cref="ReinstateUserAsync"/>), never an account action.
    /// </summary>
    private static AccountStatusEnum ToAccountStatus(ModeratorActionType action) => action switch
    {
        ModeratorActionType.WarnUser => AccountStatusEnum.Warned,
        ModeratorActionType.SuspendUser => AccountStatusEnum.Suspended,
        ModeratorActionType.BanUser => AccountStatusEnum.Banned,
        ModeratorActionType.ReinstateUser => throw new ModerationValidationException(
            ["Reinstating an account is its own action — use Reinstate."]),
        _ => throw new ModerationValidationException([$"'{action}' is not an account action."])
    };

    /// <summary>
    /// The account-status transition table (service §2.1.3; <c>layer2-services.md</c> §"Moderation
    /// Services"), checked before any mutation. A <em>live</em> suspension is <c>Suspended</c> with an end
    /// date still in the future. Banned is left only via Reinstate (literal §2.1.3). Two refusals are
    /// derived, not owner text (<c>roadmap.md</c> decision row 20): Warn on a live suspension (the same
    /// silent lowering §2.1.3 names for a ban) and Ban on Banned (a duplicate type-74 row, since D4
    /// exempts 74 from dedup). The report-driven path never reaches the Ban-on-Banned arm: there, a
    /// standing ban answers the report instead (see <see cref="ApplyAccountActionAsync"/>).
    /// </summary>
    private static void EnsureLegalAccountTransition(User target, ModeratorActionType action,
        DateTime? suspendedUntilUtc, DateTime now)
    {
        bool banned = target.AccountStatus == AccountStatusEnum.Banned;
        bool liveSuspension = target.AccountStatus == AccountStatusEnum.Suspended
                              && target.SuspendedUntilUtc > now;

        switch (action)
        {
            case ModeratorActionType.WarnUser:
                if (banned)
                    throw new ModerationValidationException([BannedLeavableOnlyByReinstate]);
                if (liveSuspension)
                    throw new ModerationValidationException(
                    [
                        $"This account is suspended until {target.SuspendedUntilUtc:yyyy-MM-dd HH:mm} UTC. A " +
                        "warning would lift the suspension — change its end date with Suspend, or Reinstate first."
                    ]);
                break;

            case ModeratorActionType.SuspendUser:
                if (suspendedUntilUtc is not DateTime until || until <= now)
                    throw new ModerationValidationException(["Choose a suspension end date in the future."]);
                if (banned)
                    throw new ModerationValidationException([BannedLeavableOnlyByReinstate]);
                break;

            case ModeratorActionType.BanUser:
                if (banned)
                    throw new ModerationValidationException(["This account is already banned."]);
                break;
        }
    }

    /// <summary>
    /// Writes the status. <c>SuspendedUntilUtc</c> is set only when the resulting status is
    /// <c>Suspended</c> and cleared otherwise, so "set only while Suspended" holds for every row a
    /// moderator action writes (service §2.1.3(c)).
    /// </summary>
    private static void ApplyStatus(User target, AccountStatusEnum newStatus, DateTime? suspendedUntilUtc)
    {
        target.AccountStatus = newStatus;
        target.SuspendedUntilUtc = newStatus == AccountStatusEnum.Suspended ? suspendedUntilUtc : null;
    }

    /// <summary>
    /// Kill any already-open session for Suspend/Ban (not Warn — a warning must not log the user out) —
    /// WU38a. IdentityRevalidatingAuthenticationStateProvider re-checks the security stamp every 30
    /// minutes; a mismatch ends the live circuit, and the next sign-in attempt is blocked by
    /// CanalaveSignInManager.CanSignInAsync.
    /// </summary>
    private async Task BumpSecurityStampIfEjectingAsync(User targetUser, AccountStatusEnum newStatus)
    {
        if (newStatus is not (AccountStatusEnum.Suspended or AccountStatusEnum.Banned)) return;

        IdentityResult result = await userManager.UpdateSecurityStampAsync(targetUser);
        if (!result.Succeeded)
            logger.LogWarning("Security stamp bump failed for user {UserId}: {Errors}", targetUser.Id,
                string.Join("; ", result.Errors.Select(e => e.Code)));
    }

    /// <summary>
    /// The post-commit half of both account-action entry points: the target's 72/73/74. A
    /// report-driven action can land on the acting moderator (a report against their own story); like
    /// every band call site, they get no notification about their own act (the D4 guardrail's general
    /// rule). The moderator-initiated path already refuses a self-target.
    /// </summary>
    private async Task NotifyAccountActionAsync(int targetUserId, ModeratorActionType action, int actingModeratorId)
    {
        if (targetUserId == actingModeratorId) return;

        await NotifyBestEffortAsync(() => action switch
        {
            ModeratorActionType.WarnUser    => notifications.NotifyAccountWarningAsync(targetUserId),
            ModeratorActionType.SuspendUser => notifications.NotifyAccountSuspendedAsync(targetUserId),
            ModeratorActionType.BanUser     => notifications.NotifyAccountBannedAsync(targetUserId),
            _ => Task.CompletedTask
        }, $"Account action ({action})", targetUserId);
    }

    /// <summary>One best-effort notification: failures are logged and swallowed, never rethrown, and
    /// each call gets its own try so one failure cannot drop the next.</summary>
    private async Task NotifyBestEffortAsync(Func<Task> notify, string what, long subjectId)
    {
        try { await notify(); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Notification} notification failed for {SubjectId}", what, subjectId);
        }
    }

    /// <summary>
    /// The account answerable for a reported artifact (owner ruling D8): <c>User</c> → that user if the
    /// row exists; <c>Message</c> → its sender; Story/Comment/BlogPost/Recommendation → the author. NULL =
    /// no row, or an anonymous/deleted author. Read on the unfiltered write context with projections (no
    /// entity load). The default arm THROWS, so a new <see cref="ReportedEntityType"/> member fails loudly
    /// until it gets its arm (WU-UserDeletion adds <c>Group</c> → <c>groups.creator_id</c>).
    /// </summary>
    private async Task<int?> ResolveAnswerableUserIdAsync(ReportedEntityType type, long id) => type switch
    {
        ReportedEntityType.User =>
            await writeDb.Users.AnyAsync(u => u.Id == (int)id) ? (int?)id : null,
        // PrivateMessage.SenderUserId is SetNull on account deletion, hence nullable.
        ReportedEntityType.Message => (await writeDb.PrivateMessages
            .Where(m => m.MessageId == id).Select(m => new { m.SenderUserId }).SingleOrDefaultAsync())?.SenderUserId,
        ReportedEntityType.Story => (await writeDb.Stories
            .Where(s => s.StoryId == (int)id).Select(s => new { s.AuthorId }).SingleOrDefaultAsync())?.AuthorId,
        ReportedEntityType.Comment => (await writeDb.BaseComments
            .Where(c => c.CommentId == id).Select(c => new { c.UserId }).SingleOrDefaultAsync())?.UserId,
        ReportedEntityType.BlogPost => (await writeDb.BlogPosts
            .Where(b => b.BlogPostId == (int)id).Select(b => new { b.AuthorId }).SingleOrDefaultAsync())?.AuthorId,
        ReportedEntityType.Recommendation => (await writeDb.Recommendations
            .Where(r => r.RecommendationId == (int)id).Select(r => new { r.RecommenderId }).SingleOrDefaultAsync())?.RecommenderId,
        _ => throw new InvalidOperationException($"No answerable-account rule for {type}"),
    };

    /// <summary>
    /// Which user an account action lands on, derived from the report: the throwing form of
    /// <see cref="ResolveAnswerableUserIdAsync"/>. Throws <see cref="ModerationValidationException"/> — a
    /// user-facing type — when no account can be resolved, so the moderator is told why instead of
    /// getting the generic error. Submission never uses this form: anonymous or deleted-author content
    /// stays reportable.
    /// </summary>
    private async Task<int> ResolveActionTargetUserIdAsync(ReportedEntityType type, long id) =>
        await ResolveAnswerableUserIdAsync(type, id)
        ?? throw new ModerationValidationException([NoAccountToActOn]);

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength];

    /// <summary>
    /// Loads the <see cref="IModeratableContent"/> entity for the given type and id from the
    /// write context (unfiltered — sees ground truth regardless of takedown/rating state).
    /// ContentRating and GroupAudience do not apply on the write context; a moderator acting on
    /// a taken-down or rating-gated entity acts on ground truth, not the public view.
    /// Returns <c>null</c> if the entity doesn't exist.
    /// Message and User are not IModeratableContent — callers handle them directly.
    /// </summary>
    private async Task<IModeratableContent?> LoadModeratableAsync(ReportedEntityType type, long id)
    {
        switch (type)
        {
            case ReportedEntityType.Story:
                return await writeDb.Stories
                    .SingleOrDefaultAsync(s => s.StoryId == (int)id);

            case ReportedEntityType.Comment:
                return await writeDb.BaseComments
                    .SingleOrDefaultAsync(c => c.CommentId == id);

            case ReportedEntityType.BlogPost:
                return await writeDb.BlogPosts
                    .SingleOrDefaultAsync(b => b.BlogPostId == (int)id);

            case ReportedEntityType.Recommendation:
                return await writeDb.Recommendations
                    .SingleOrDefaultAsync(r => r.RecommendationId == (int)id);

            default:
                throw new InvalidOperationException($"LoadModeratableAsync does not handle '{type}' — use the direct path.");
        }
    }

    /// <summary>
    /// Kind (g) for report submission (settled 2026-07-26). Two rules, deliberately different:
    /// <list type="bullet">
    /// <item><b>Existence is always required</b> — checked on the unfiltered write context, so a
    /// taken-down target still counts as existing.</item>
    /// <item><b>Visibility is required except for takedown</b> — a reporter who legitimately opened
    /// content that a moderator removed a moment later must still be able to file. Everything else
    /// (draft/unpublished, non-public story status, rating without consent, Private profile,
    /// M-audience group) must be refused.</item>
    /// </list>
    /// Throws <see cref="KeyNotFoundException"/> either way so a hidden target and an absent one are
    /// indistinguishable. <c>Message</c> is participant-scoped and validated by its own path.
    /// </summary>
    private async Task RequireReportableTargetAsync(ReportedEntityType type, long id)
    {
        KeyNotFoundException NotFound() =>
            new($"The reported {type} could not be found.");

        // Existence on the unfiltered write context — a taken-down row is still a real row.
        bool exists = type switch
        {
            ReportedEntityType.Story => await writeDb.Stories.AnyAsync(s => s.StoryId == (int)id),
            ReportedEntityType.User => await writeDb.Users.AnyAsync(u => u.Id == (int)id),
            ReportedEntityType.Comment => await writeDb.BaseComments.AnyAsync(c => c.CommentId == id),
            ReportedEntityType.BlogPost => await writeDb.BlogPosts.AnyAsync(b => b.BlogPostId == (int)id),
            ReportedEntityType.Recommendation =>
                await writeDb.Recommendations.AnyAsync(r => r.RecommendationId == (int)id),
            ReportedEntityType.Message => await writeDb.PrivateMessages.AnyAsync(m => m.MessageId == id),
            _ => false,
        };
        if (!exists) throw NotFound();

        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        // Takedown exemption: if the row is gone from the read context purely because IsTakenDown is
        // set, the report is still legitimate. Probing with that one filter lifted separates
        // "removed by a moderator" from every other reason the target might be hidden.
        bool takenDownOnly = type switch
        {
            ReportedEntityType.Story => await readDb.Stories
                .IgnoreQueryFilters(["IsTakenDown"]).AnyAsync(s => s.StoryId == (int)id && s.IsTakenDown),
            ReportedEntityType.Comment => await readDb.BaseComments
                .IgnoreQueryFilters(["IsTakenDown"]).AnyAsync(c => c.CommentId == id && c.IsTakenDown),
            ReportedEntityType.BlogPost => await readDb.BlogPosts
                .IgnoreQueryFilters(["IsTakenDown"]).AnyAsync(b => b.BlogPostId == (int)id && b.IsTakenDown),
            ReportedEntityType.Recommendation => await readDb.Recommendations
                .IgnoreQueryFilters(["IsTakenDown"]).AnyAsync(r => r.RecommendationId == (int)id && r.IsTakenDown),
            _ => false,
        };
        if (takenDownOnly) return;

        bool visible = type switch
        {
            ReportedEntityType.Story =>
                await StoryVisibilityGuard.IsStoryVisibleAsync(readDb, ActiveUser, (int)id),
            ReportedEntityType.BlogPost =>
                await BlogPostVisibilityGuard.IsBlogPostVisibleAsync(readDb, ActiveUser, (int)id),
            ReportedEntityType.User =>
                await ProfileVisibilityGuard.IsProfileVisibleAsync(readDb, ActiveUser, (int)id),
            ReportedEntityType.Comment => await readDb.BaseComments.AnyAsync(c => c.CommentId == id),
            ReportedEntityType.Recommendation =>
                await readDb.Recommendations.AnyAsync(r => r.RecommendationId == (int)id),
            // Messages are participant-scoped, not content-visibility-scoped; existence above is the
            // check, and a non-participant cannot learn a message id through any read path.
            ReportedEntityType.Message => true,
            _ => false,
        };
        if (!visible) throw NotFound();
    }

    /// <summary>
    /// Atomically increments (positive delta) or decrements (negative delta) the
    /// <c>ActiveReportCount</c> column on the target entity — the single authority on mutating it.
    /// The column is a cache of the target's open-report <c>COUNT(*)</c> (owner ruling D7). No-op for
    /// <c>Message</c> (PrivateMessage has no counter column). Uses ExecuteUpdateAsync (set-based, no
    /// load) on the unfiltered write context, so taken-down content gets its counter adjusted correctly.
    /// </summary>
    private async Task AdjustActiveReportCountAsync(ReportedEntityType type, long id, int delta)
    {
        switch (type)
        {
            case ReportedEntityType.Story:
                await writeDb.Stories
                    .Where(s => s.StoryId == (int)id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.ActiveReportCount, x => x.ActiveReportCount + delta));
                break;
            case ReportedEntityType.User:
                await writeDb.Users
                    .Where(u => u.Id == (int)id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.ActiveReportCount, x => x.ActiveReportCount + delta));
                break;
            case ReportedEntityType.Comment:
                await writeDb.BaseComments
                    .Where(c => c.CommentId == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.ActiveReportCount, x => x.ActiveReportCount + delta));
                break;
            case ReportedEntityType.BlogPost:
                await writeDb.BlogPosts
                    .Where(b => b.BlogPostId == (int)id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.ActiveReportCount, x => x.ActiveReportCount + delta));
                break;
            case ReportedEntityType.Recommendation:
                await writeDb.Recommendations
                    .Where(r => r.RecommendationId == (int)id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.ActiveReportCount, x => x.ActiveReportCount + delta));
                break;
            case ReportedEntityType.Message:
                // PrivateMessage has no ActiveReportCount column; skip per settled WU34 convention.
                break;
        }
    }

    /// <summary>
    /// Soft-removes the target by setting <c>IsTakenDown = true</c> and recording removal metadata
    /// via <see cref="IModeratableContent"/>. Returns the content author's user id, or <c>null</c> when
    /// the entity doesn't exist.
    /// Messages have no takedown columns — falls through to <see cref="ApplyHardDeleteAsync"/>.
    /// User targets never reach here: <see cref="ResolveWithRemovalAsync"/> refuses them (an account
    /// action resolves a User report).
    /// </summary>
    private async Task<int?> ApplyRemovalAsync(ReportedEntityType type, long id, string reason)
    {
        if (type == ReportedEntityType.Message)
            return await ApplyHardDeleteAsync(type, id);

        if (type == ReportedEntityType.User)
            return null; // Unreachable from ResolveWithRemovalAsync (it refuses User targets).

        IModeratableContent? entity = await LoadModeratableAsync(type, id);
        if (entity is null) return null;

        entity.IsTakenDown = true;
        entity.TakedownDate = DateTime.UtcNow;
        entity.TakedownReason = reason;

        // Owner ruling D3, removal trigger 5: a taken-down recommendation can't be shown as a reminder
        // or collect credit, so every reader's attribution naming it is staged for deletion — committed
        // by the caller's SaveChangesAsync with the takedown. A takedown reversal doesn't restore them.
        if (type == ReportedEntityType.Recommendation)
            await RecommendationAttribution.StageSweepAsync(writeDb, (int)id);

        return entity.AuthorUserId;
    }

    /// <summary>
    /// Hard-deletes the target entity (illegal content path — CSAM, piracy). Returns the author's
    /// user id before deletion when possible, or <c>null</c>. The <c>reports</c> rows survive it (they
    /// carry no FK to their target) and are closed by the caller (D7).
    /// <para><b>TPT dependents go first (owner ruling D10).</b> A Story's chapter comments and a blog
    /// post's comments and polls are deleted through their base rows by <see cref="TptDelete"/> — no
    /// cascade from a content parent reaches a TPT base row, and the parent → child FKs are RESTRICT,
    /// so the save would otherwise fail. That SQL runs at once while the <c>Remove</c> waits for the
    /// caller's <c>SaveChangesAsync</c>; both sit inside <see cref="InResolveTransactionAsync{T}"/>'s
    /// transaction. A Comment, Recommendation or Message is a loaded entity whose own removal deletes
    /// every row it owns (a <c>BaseComment</c> removal deletes base and child rows).</para>
    /// </summary>
    private async Task<int?> ApplyHardDeleteAsync(ReportedEntityType type, long id)
    {
        if (type is ReportedEntityType.Story or ReportedEntityType.Comment
            or ReportedEntityType.BlogPost or ReportedEntityType.Recommendation)
        {
            IModeratableContent? entity = await LoadModeratableAsync(type, id);
            if (entity is null) return null;
            int? authorId = entity.AuthorUserId;

            if (type == ReportedEntityType.Story)
                await TptDelete.StoryCommentsAsync(writeDb, (int)id);
            else if (type == ReportedEntityType.BlogPost)
                await TptDelete.BlogPostDependentsAsync(writeDb, (int)id);

            writeDb.Remove((object)entity);
            return authorId;
        }

        if (type == ReportedEntityType.Message)
        {
            var msg = await writeDb.PrivateMessages
                .SingleOrDefaultAsync(m => m.MessageId == id);
            if (msg is null) return null;
            writeDb.PrivateMessages.Remove(msg);
            return null;
        }

        throw new InvalidOperationException($"Hard delete is not supported for entity type '{type}'.");
    }
}
