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
/// One user's moderation record for <c>/mod/users/{UserId}</c> — current account standing plus the
/// reports filed <i>against that user</i>.
/// <para><b>Scope caveat, surfaced on the page itself:</b> <see cref="Reports"/> holds reports whose
/// target is this user, NOT reports against content they authored. Resolving the latter means
/// author-lookup across four content tables and is deliberately deferred (see
/// <c>audit/Moderation.md</c> §"WU-UserModeration settled constraints"). A moderator reading this
/// view must not treat an empty list as "no complaints about this person."</para>
/// </summary>
public record UserModerationHistoryDto(
    int UserId,
    string Username,
    string? AvatarUrl,
    AccountStatusEnum AccountStatus,
    /// <summary>Set only while <see cref="AccountStatus"/> is <c>Suspended</c>; UTC.</summary>
    DateTime? SuspendedUntilUtc,
    int ActiveReportCount,
    IReadOnlyList<ReportQueueItemDto> Reports);

/// <summary>Pending-approval story row for the /mod/submissions queue.</summary>
public record StorySubmissionQueueItemDto(
    int StoryId,
    string Title,
    string AuthorUserName,
    Rating Rating,
    DateTime SubmittedDate,
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
}
