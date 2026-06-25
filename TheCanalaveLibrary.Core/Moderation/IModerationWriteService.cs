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
    /// Sets <c>User.AccountStatus</c>, records on the report, and notifies the target user.
    /// </summary>
    Task ApplyAccountActionAsync(long reportId, ModeratorActionType action,
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
