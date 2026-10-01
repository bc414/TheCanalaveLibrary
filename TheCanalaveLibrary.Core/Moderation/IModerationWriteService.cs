using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Write side of the Moderation feature cluster: the moderator actions. Inherits the read interface
/// (CQRS-lite with write-inherits-read pattern). Report submission is not here — it is the member-facing
/// <see cref="IReportSubmissionService"/> (owner ruling D9). Every member requires the Moderator or
/// Admin role, enforced in the service. Every outcome notification this interface sends is
/// null-sourced (D5): the acting moderator is never named to the recipient, and never notified of
/// their own act.
/// </summary>
public interface IModerationWriteService : IModerationReadService
{
    // ── Moderator queue actions (Feature 47) ─────────────────────────────────────

    /// <summary>
    /// Claims a report for this moderator (<c>UnderReview</c> status).
    /// </summary>
    Task ClaimReportAsync(long reportId);

    /// <summary>
    /// Resolves a report with no action taken. Decrements the target's
    /// <c>ActiveReportCount</c> and notifies the reporter. Closes no other report (owner ruling D7:
    /// one moderator's "no" is a ruling on one complaint).
    /// <para>Every resolve path locks the report row and refuses one that is no longer Open or
    /// UnderReview: <see cref="ModerationValidationException"/> ("already resolved"); an unknown report
    /// id is <see cref="KeyNotFoundException"/> (service §2.1.2).</para>
    /// </summary>
    Task ResolveNoActionAsync(long reportId, string? actionNotes);

    /// <summary>
    /// Resolves a report with a content-removal action. Soft-hides the target (default) or
    /// hard-deletes it (illegal-content path, <paramref name="hardDelete"/> = true).
    /// <para>Owner ruling D7: in the same transaction, closes every other Open/UnderReview report on the
    /// same <c>(ReportedEntityType, ReportedEntityId)</c> target as <c>ResolvedActionTaken</c>, moves the
    /// target's <c>ActiveReportCount</c> by −(1 + reports closed), and notifies the reporter, every
    /// distinct sibling reporter, and the content author. A <c>User</c>-targeted report cannot be
    /// resolved by removal (<see cref="ModerationValidationException"/> — use an account action). Same
    /// lock and status guard as <see cref="ResolveNoActionAsync"/>.</para>
    /// </summary>
    Task ResolveWithRemovalAsync(long reportId, string removalReason, bool hardDelete = false);

    /// <summary>
    /// Applies an account action (warn / suspend / ban) without removing specific content.
    /// Sets <c>User.AccountStatus</c>, resolves the report as <c>ResolvedActionTaken</c>, decrements
    /// the target's <c>ActiveReportCount</c>, and notifies the target user.
    /// <para><b>Which user is acted on</b> is resolved from the report, not assumed: a
    /// <c>User</c>-targeted report acts on that user; a Story/Comment/BlogPost/Recommendation report
    /// acts on the reported content's author; a Message report acts on its sender. Throws
    /// <see cref="ModerationValidationException"/> when no author can be resolved (anonymous or
    /// deleted). Supersedes the WU34 rule that required the report target to be a User — see
    /// <c>layer2-services.md</c> §"Account actions — target resolution and the report-as-audit-record
    /// rule".</para>
    /// <para>Same lock and status guard as <see cref="ResolveNoActionAsync"/>; enforces the
    /// account-status transition table (a suspension needs a future end date; nothing but
    /// <see cref="ReinstateUserAsync"/> leaves Banned; no Warn over a live suspension —
    /// <see cref="ModerationValidationException"/>); refuses <see cref="ModeratorActionType.ReinstateUser"/>.
    /// A Ban on an already-banned account is not refused here: the standing ban answers the report
    /// (resolved with action taken; no status write, stamp bump or second ban notification).
    /// Tells a member reporter the outcome (<c>ReportResolved</c>, report id). Closes no other report
    /// (D7).</para>
    /// </summary>
    Task ApplyAccountActionAsync(long reportId, ModeratorActionType action,
        string reason, DateTime? suspendedUntilUtc = null);

    /// <summary>
    /// Applies an account action to a user with no existing report — the moderator-initiated path
    /// behind <c>/mod/users/{UserId}</c>.
    /// <para>Opens and resolves a <c>Report</c> in the same unit of work so the action still leaves
    /// the standard audit record (<c>Report</c> IS the audit record — there is no separate
    /// moderation-action table). <c>ReporterUserId == ModeratorUserId</c> is what marks the row as
    /// moderator-initiated; <paramref name="reasonId"/> is a real seeded <c>ReportReason</c>, chosen
    /// by the moderator. Because the row opens and resolves together, <c>ActiveReportCount</c> is
    /// deliberately untouched (no +1/-1 pair). Same transition table as
    /// <see cref="ApplyAccountActionAsync"/>, and a second Ban is refused here (there is no report for a
    /// standing ban to answer); sends no report receipt (the D4 guardrail).</para>
    /// </summary>
    Task ApplyAccountActionToUserAsync(int targetUserId, short reasonId, ModeratorActionType action,
        string reason, DateTime? suspendedUntilUtc = null);

    /// <summary>
    /// Returns a Warned, Suspended or Banned user to <c>Active</c> and clears
    /// <c>SuspendedUntilUtc</c> — the only path out of Banned, and the only writer of <c>Active</c>
    /// (service §2.1.3(b)). Files a moderator-initiated <c>Report</c> as the audit record (reason
    /// "Other", <c>Notes = ActionTaken = reason</c>), same shape as
    /// <see cref="ApplyAccountActionToUserAsync"/>; <c>ActiveReportCount</c> is untouched. No security
    /// stamp bump and no notification (no type exists — tracker F14).
    /// <para>Throws <see cref="UnauthorizedAccessException"/> for a non-moderator,
    /// <see cref="KeyNotFoundException"/> for an unknown user, and
    /// <see cref="ModerationValidationException"/> for a self-target, a blank or over-long reason, or a
    /// user who is already Active.</para>
    /// </summary>
    Task ReinstateUserAsync(int targetUserId, string reason);

    // ── Submission approval (Feature 48) ─────────────────────────────────────────

    /// <summary>
    /// Approves a <c>PendingApproval</c> story (WU-StoryLifecycle, D1): sets
    /// <c>StoryStatusId = PostApprovalStatus</c>, stamps <c>PublishedDate</c> if the story was never
    /// published (D2), and adds 1 to the author's monotonic <c>ApprovedStorySubmissions</c> — the
    /// status flip and the increment commit in one transaction. Fires <c>StoryApproved</c>
    /// best-effort after commit.
    /// <para>Throws <see cref="KeyNotFoundException"/> for an unknown story, and
    /// <see cref="ModerationValidationException"/> when the story is no longer
    /// <c>PendingApproval</c> (handled elsewhere — checked again inside the conditional update), it is
    /// taken down (a taken-down story's status is frozen), its <c>PostApprovalStatus</c> is not an
    /// entry status (InProgress/Completed/OpenBeta), or its author is not live (deleted, banned, or
    /// suspended with a null or future end date).</para>
    /// </summary>
    Task ApproveStoryAsync(int storyId);

    /// <summary>
    /// Rejects a <c>PendingApproval</c> story: sets <c>StoryStatusId = Rejected</c>, records the
    /// reason, and fires <c>StoryRejected</c> best-effort. <c>Rejected</c> is reachable only from
    /// <c>PendingApproval</c>. Unguarded on the author, so the queue can always be cleared.
    /// <para>Throws <see cref="KeyNotFoundException"/> for an unknown story and
    /// <see cref="ModerationValidationException"/> when it is no longer <c>PendingApproval</c> or is
    /// taken down (rejecting would overwrite the takedown's own reason and date).</para>
    /// </summary>
    Task RejectStoryAsync(int storyId, string reason);

    /// <summary>
    /// Revokes (<paramref name="canAutoApprove"/> = false) or restores a user's
    /// <c>CanAutoApprove</c> flag (WU-StoryLifecycle, D1). Files a moderator-initiated <c>Report</c>
    /// as the audit record, same shape as <see cref="ApplyAccountActionToUserAsync"/>. An unchanged
    /// value is a no-op that writes no row. No notification.
    /// <para>Throws <see cref="UnauthorizedAccessException"/> for a non-moderator,
    /// <see cref="KeyNotFoundException"/> for an unknown user, and
    /// <see cref="ModerationValidationException"/> for a self-target, an unknown reason, or a
    /// missing or over-long reason.</para>
    /// </summary>
    Task SetCanAutoApproveAsync(int targetUserId, bool canAutoApprove, short reasonId, string reason);
}
