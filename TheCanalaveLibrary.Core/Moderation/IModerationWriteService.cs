using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Write side of the Moderation feature cluster. Inherits the read interface
/// (CQRS-lite with write-inherits-read pattern).
/// </summary>
public interface IModerationWriteService : IModerationReadService
{
    // ── Report submission (Feature 46) ────────────────────────────────────────────

    /// <summary>
    /// Submits a report against a content item or user. Validates the target-type allow-set,
    /// increments the target's <c>ActiveReportCount</c>, and fires a best-effort
    /// <c>ReportReceived</c> notification to the reporter.
    /// </summary>
    Task SubmitReportAsync(SubmitReportRequest request);

    // ── Moderator queue actions (Feature 47) ─────────────────────────────────────

    /// <summary>
    /// Claims a report for this moderator (<c>UnderReview</c> status).
    /// </summary>
    Task ClaimReportAsync(long reportId);

    /// <summary>
    /// Resolves a report with no action taken. Decrements the target's
    /// <c>ActiveReportCount</c> and notifies the reporter.
    /// </summary>
    Task ResolveNoActionAsync(long reportId, string? actionNotes);

    /// <summary>
    /// Resolves a report with a content-removal action. Soft-hides the target (default) or
    /// hard-deletes it (illegal-content path, <paramref name="hardDelete"/> = true).
    /// Decrements <c>ActiveReportCount</c>, notifies reporter and content author.
    /// </summary>
    Task ResolveWithRemovalAsync(long reportId, string removalReason, bool hardDelete = false);

    /// <summary>
    /// Applies an account action (warn / suspend / ban) without removing specific content.
    /// Sets <c>User.AccountStatus</c>, resolves the report as <c>ResolvedActionTaken</c>, decrements
    /// the target's <c>ActiveReportCount</c>, and notifies the target user.
    /// <para><b>Which user is acted on</b> is resolved from the report, not assumed: a
    /// <c>User</c>-targeted report acts on that user; a Story/Comment/BlogPost/Recommendation report
    /// acts on the reported content's author; a Message report acts on its sender. Throws
    /// <c>CanalaveValidationException</c> when no author can be resolved (anonymous or deleted).
    /// Supersedes the WU34 rule that required the report target to be a User — see
    /// <c>layer2-services.md</c> §"Account actions — target resolution and the report-as-audit-record
    /// rule".</para>
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
    /// deliberately untouched (no +1/-1 pair).</para>
    /// </summary>
    Task ApplyAccountActionToUserAsync(int targetUserId, short reasonId, ModeratorActionType action,
        string reason, DateTime? suspendedUntilUtc = null);

    // ── Submission approval (Feature 48) ─────────────────────────────────────────

    /// <summary>
    /// Approves a <c>PendingApproval</c> story: sets <c>StoryStatusId = PostApprovalStatus</c>
    /// and fires <c>StoryApproved</c> notification to the author.
    /// </summary>
    Task ApproveStoryAsync(int storyId);

    /// <summary>
    /// Rejects a <c>PendingApproval</c> story: sets <c>StoryStatusId = Rejected</c>,
    /// records the reason, and fires <c>StoryRejected</c> notification to the author.
    /// </summary>
    Task RejectStoryAsync(int storyId, string reason);
}
