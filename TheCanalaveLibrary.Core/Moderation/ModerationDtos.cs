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
