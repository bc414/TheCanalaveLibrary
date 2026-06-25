namespace TheCanalaveLibrary.Core;

/// <summary>
/// Lightweight DTO for rendering a <c>GroupCard</c> in the <c>/groups</c> listing.
/// <see cref="AudienceType"/> is derived at projection time via
/// <see cref="GroupAudienceTypeMapper.FromRatings"/>; only audience-accessible groups are ever
/// projected here (the <c>GroupAudience</c> named query filter already excludes hidden groups).
/// </summary>
public record GroupCardDto(
    int GroupId,
    string GroupName,
    string? Description,
    GroupAudienceType AudienceType,
    int MemberCount,
    DateTime DateCreated);
