namespace TheCanalaveLibrary.Core;

/// <summary>
/// One credit the caller AUTHORED (the caller owns <see cref="StoryId"/>), shown with its current
/// <see cref="Status"/> on the owner-wide management page (<c>/acknowledgments</c>). Elevated,
/// owner-scoped read — ignores ContentRating/IsTakenDown so an author can always manage their own
/// credits, mirroring <see cref="StoryLineageOutgoingDto"/>.
/// </summary>
public record StoryAcknowledgmentOutgoingDto(
    int StoryId,
    string StoryTitle,
    int AcknowledgedUserId,
    string? AcknowledgedUserName,
    short RoleId,
    string RoleName,
    StoryAcknowledgmentStatus Status);

/// <summary>
/// One incoming <see cref="StoryAcknowledgmentStatus.Pending"/> credit where the caller is the
/// RECIPIENT (<see cref="AcknowledgmentRoleId"/> row's <c>AcknowledgedUserId</c> is the caller) —
/// the approval-inbox half of the owner-wide management page. <see cref="CreditingAuthorId"/>/
/// <see cref="CreditingAuthorName"/> is the story's author (nullable — a deleted account leaves the
/// request orphaned but still visible/declinable).
/// </summary>
public record StoryAcknowledgmentIncomingRequestDto(
    int StoryId,
    string StoryTitle,
    int? CreditingAuthorId,
    string? CreditingAuthorName,
    short RoleId,
    string RoleName);

/// <summary>
/// Aggregated data for the owner-wide <c>/acknowledgments</c> management page
/// (WU-StatBadgeProducers) — mirrors <c>MyStoryLineagesPage</c>'s single-fetch shape.
/// <see cref="Outgoing"/> spans every story the caller authored; <see cref="IncomingRequests"/>
/// spans every Pending credit naming the caller as the recipient.
/// </summary>
public record StoryAcknowledgmentManageDto(
    IReadOnlyList<StoryAcknowledgmentOutgoingDto> Outgoing,
    IReadOnlyList<StoryAcknowledgmentIncomingRequestDto> IncomingRequests);
