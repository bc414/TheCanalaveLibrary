namespace TheCanalaveLibrary.Core;

/// <summary>Submitted by a user to report a content item or another user.</summary>
public record SubmitReportRequest(
    ReportedEntityType EntityType,
    long EntityId,
    short ReasonId,
    string? Notes);

/// <summary>Lookup row for populating the reason dropdown in <c>ReportDialog</c>.</summary>
public record ReportReasonDto(short ReasonId, string ReasonName, string? Description);

/// <summary>A single row in the moderator report queue.</summary>
public record ReportQueueItemDto(
    long ReportId,
    ReportedEntityType EntityType,
    long EntityId,
    /// <summary>Human-readable label resolved from the target entity (title, username, etc.).</summary>
    string TargetLabel,
    /// <summary>Deep-link to the reported entity; null when not navigable (e.g. a deleted item).</summary>
    string? TargetUrl,
    string ReasonName,
    string? Notes,
    ReportStatusEnum Status,
    /// <summary>Username of the reporter; null for anonymous reports.</summary>
    string? ReporterUserName,
    int? ModeratorUserId,
    string? ActionTaken,
    DateTime DateReported,
    DateTime? DateResolved,
    /// <summary>ActiveReportCount on the target entity — used for triage ordering.</summary>
    int TargetActiveReportCount);

/// <summary>
/// Used by moderator action endpoints to carry the action type + optional notes.
/// </summary>
public record ModeratorActionRequest(
    ModeratorActionType ActionType,
    string? Reason);

/// <summary>
/// One user's moderation record for <c>/mod/users/{UserId}</c> — current account standing plus every
/// report this user was answerable for when it was filed.
/// <para><see cref="Reports"/> reads <c>Report.ReportedUserId</c> (owner ruling D8, tracker B18): reports
/// about the account itself <em>and</em> about content they wrote or messages they sent, every target
/// type, each row carrying its own <see cref="ReportQueueItemDto.EntityType"/>. Moderator-filed actions
/// (warn/suspend/ban, auto-approve, reinstate) appear too. A row whose target has since been deleted is
/// kept and labelled <c>[deleted {type}]</c>. (Until WU-ModerationIntegrity, 2026-09-30, this held only
/// user-targeted reports and the page carried a caveat saying so.)</para>
/// </summary>
public record UserModerationHistoryDto(
    int UserId,
    string Username,
    string? AvatarUrl,
    AccountStatusEnum AccountStatus,
    /// <summary>Set only while <see cref="AccountStatus"/> is <c>Suspended</c> — every moderator action
    /// clears it otherwise (service §2.1.3); UTC.</summary>
    DateTime? SuspendedUntilUtc,
    int ActiveReportCount,
    IReadOnlyList<ReportQueueItemDto> Reports,
    /// <summary>Story-approval trust (WU-StoryLifecycle, D1): moderator approvals recorded for this
    /// author. Monotonic.</summary>
    int ApprovedStorySubmissions = 0,
    /// <summary>Whether the trust waiver is in force (a moderator may revoke/restore it).</summary>
    bool CanAutoApprove = true);

/// <summary>Pending-approval story row for the /mod/submissions queue.</summary>
public record StorySubmissionQueueItemDto(
    int StoryId,
    string Title,
    string AuthorUserName,
    Rating Rating,
    /// <summary>When the story last entered the queue (<c>Story.SubmittedDate</c>); null only for a
    /// row that predates the column and had no date to carry over.</summary>
    DateTime? SubmittedDate,
    /// <summary>The status the story will move to if approved (set by the author at submission time).</summary>
    StoryStatusEnum PostApprovalStatus,
    bool IsImportedWork);

/// <summary>Types of moderator actions that can be applied to a report.</summary>
public enum ModeratorActionType
{
    Claim,
    ResolveNoAction,
    ResolveActionTaken,
    SoftRemoveContent,
    HardDeleteContent,
    WarnUser,
    SuspendUser,
    BanUser,
    /// <summary>Returns a non-Active user to Active (<c>IModerationWriteService.ReinstateUserAsync</c>
    /// only — the two account-action methods refuse it). Appended last: the enum travels by name in the
    /// query string, and existing values keep their ordinals.</summary>
    ReinstateUser,
}
