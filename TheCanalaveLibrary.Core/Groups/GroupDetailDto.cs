namespace TheCanalaveLibrary.Core;

/// <summary>
/// Full display DTO for the group page (<c>/group/{GroupId}/{*Slug}</c>).
/// <see cref="CreatorDisplayName"/> is nullable (creator may be deleted — SET NULL on User delete).
/// <see cref="CurrentUserRole"/> is null when the viewer is not a member (anonymous or non-member).
/// <see cref="FolderTree"/> is the root-level folders; each folder carries its own nested children.
/// </summary>
public record GroupDetailDto(
    int GroupId,
    string GroupName,
    string? Description,
    GroupAudienceType AudienceType,
    Rating MaxContentRating,
    int? CreatorId,
    string? CreatorDisplayName,
    int MemberCount,
    DateTime DateCreated,
    GroupRole? CurrentUserRole,
    IReadOnlyList<GroupFolderDto> FolderTree,
    IReadOnlyList<int> StoryIds);
