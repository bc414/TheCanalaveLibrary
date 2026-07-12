namespace TheCanalaveLibrary.Core;

/// <summary>
/// One outgoing lineage link owned by the caller (the caller owns <see cref="SourceStoryId"/>),
/// shown with its current <see cref="Status"/> on the owner-wide management page
/// (<c>/story-lineages</c>, Feature 10 WU42). Includes both titles regardless of status/visibility
/// filters — the owner may manage a link even if the target has since gone mature/taken-down for
/// them (an elevated, owner-scoped read, distinct from the public-display path in
/// <see cref="StoryLineageDto"/>).
/// </summary>
public record StoryLineageOutgoingDto(
    int SourceStoryId,
    string SourceTitle,
    int TargetStoryId,
    string TargetTitle,
    short TypeId,
    string TypeName,
    StoryLineageStatus Status);

/// <summary>
/// One incoming <see cref="StoryLineageStatus.Pending"/> request targeting a story the caller owns
/// (the caller owns <see cref="TargetStoryId"/>) — the approval-inbox half of the owner-wide
/// management page. <see cref="RequesterUserId"/>/<see cref="RequesterUserName"/> are the source
/// story's author (nullable — a deleted account leaves the request orphaned but still visible/
/// rejectable, matching the SET NULL policy elsewhere on this site).
/// </summary>
public record StoryLineageIncomingRequestDto(
    int SourceStoryId,
    string SourceTitle,
    int? RequesterUserId,
    string? RequesterUserName,
    int TargetStoryId,
    string TargetTitle,
    short TypeId,
    string TypeName);

/// <summary>
/// Aggregated data for the owner-wide <c>/story-lineages</c> management page (Feature 10, WU42) —
/// mirrors <c>MySeriesPage</c>'s single-fetch shape. <see cref="Outgoing"/> spans every story the
/// caller owns (as source); <see cref="IncomingRequests"/> spans every story the caller owns (as
/// target) with a Pending request waiting on them.
/// </summary>
public record StoryLineageManageDto(
    IReadOnlyList<StoryLineageOutgoingDto> Outgoing,
    IReadOnlyList<StoryLineageIncomingRequestDto> IncomingRequests);
