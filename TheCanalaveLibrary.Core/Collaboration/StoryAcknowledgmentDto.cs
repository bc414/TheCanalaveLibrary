namespace TheCanalaveLibrary.Core;

/// <summary>
/// One "credited as {RoleName}" entry for the public story-page display (WU-StatBadgeProducers).
/// Returned only for <see cref="StoryAcknowledgmentStatus.Accepted"/> rows — a Pending credit is not
/// public until the credited user consents, and a Declined one never becomes public.
/// </summary>
public record StoryAcknowledgmentDto(
    short RoleId,
    string RoleName,
    int AcknowledgedUserId,
    string AcknowledgedUserName);

/// <summary>The seeded acknowledgment-role lookup (Beta Reader / Planner / Cover Artist / Editor —
/// role 5 "Inspiration" retired, see <c>audit/Stories.md</c>), for the credit-request form.</summary>
public record AcknowledgmentRoleDto(short RoleId, string RoleName);
