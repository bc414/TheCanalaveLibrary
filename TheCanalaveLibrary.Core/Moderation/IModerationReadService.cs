namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read side of the Moderation feature cluster (Features 46/47/48): the moderator queues and the
/// per-user history, used by the <c>InteractiveAuto</c> <c>/mod/*</c> pages (prerendered on the server,
/// then the HTTP client twin on WASM). Every member is moderator-only and the server implementation
/// enforces it itself — anonymous → <see cref="InvalidOperationException"/>, signed in without the
/// Moderator or Admin role → <see cref="UnauthorizedAccessException"/> (owner ruling D9: the page's
/// <c>[Authorize]</c> does not protect the circuit). The reason lookup a reporter needs lives on
/// <see cref="IReportSubmissionService"/>.
/// </summary>
public interface IModerationReadService
{
    /// <summary>
    /// Returns the open-report queue ordered by <c>TargetActiveReportCount</c> descending
    /// (most-reported first). <paramref name="includeResolved"/> includes closed reports too.
    /// </summary>
    Task<ReportQueueItemDto[]> GetReportQueueAsync(bool includeResolved = false);

    /// <summary>
    /// Returns stories currently in <c>PendingApproval</c> status, ordered by submission date ascending.
    /// A taken-down story is never listed — its status is frozen and approve/reject refuse it
    /// (<c>layer2-services.md</c> §"Story Lifecycle").
    /// </summary>
    Task<StorySubmissionQueueItemDto[]> GetPendingSubmissionsAsync();

    /// <summary>
    /// One user's account standing plus every report whose <c>ReportedUserId</c> is that user — reports
    /// about the account itself <em>and</em> about content they were answerable for, every target type,
    /// all statuses, newest first (owner ruling D8, tracker B18) — for the <c>/mod/users/{UserId}</c>
    /// detail view. A report whose target no longer exists is kept and labelled
    /// <c>[deleted {type}]</c>. Returns <c>null</c> when no such user exists.
    /// <para>Like the rest of this service, a moderator work surface: no ContentRating,
    /// ProfileVisibility, or takedown filtering is applied (settled 2026-07-18 — see
    /// <c>content-safety.md</c> §"Moderator review surfaces are work surfaces").</para>
    /// </summary>
    Task<UserModerationHistoryDto?> GetUserModerationHistoryAsync(int userId);
}
