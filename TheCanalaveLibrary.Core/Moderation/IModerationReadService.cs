namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read side of the Moderation feature cluster (Features 46/47/48). Used by moderator-only
/// Blazor pages; server-rendered, no WASM. Requires moderator or admin role at the call site.
/// </summary>
public interface IModerationReadService
{
    /// <summary>
    /// Returns all report-reason lookup rows for the ReportDialog dropdown.
    /// </summary>
    Task<ReportReasonDto[]> GetReportReasonsAsync();

    /// <summary>
    /// Returns the open-report queue ordered by <c>TargetActiveReportCount</c> descending
    /// (most-reported first). <paramref name="includeResolved"/> includes closed reports too.
    /// </summary>
    Task<ReportQueueItemDto[]> GetReportQueueAsync(bool includeResolved = false);

    /// <summary>
    /// Returns stories currently in <c>PendingApproval</c> status, ordered by submission date ascending.
    /// </summary>
    Task<StorySubmissionQueueItemDto[]> GetPendingSubmissionsAsync();

    /// <summary>
    /// One user's account standing plus every report targeting them (all statuses, newest first),
    /// for the <c>/mod/users/{UserId}</c> detail view. Returns <c>null</c> when no such user exists.
    /// <para>Like the rest of this service, a moderator work surface: no ContentRating,
    /// ProfileVisibility, or takedown filtering is applied (settled 2026-07-18 — see
    /// <c>content-safety.md</c> §"Moderator review surfaces are work surfaces").</para>
    /// </summary>
    Task<UserModerationHistoryDto?> GetUserModerationHistoryAsync(int userId);
}
