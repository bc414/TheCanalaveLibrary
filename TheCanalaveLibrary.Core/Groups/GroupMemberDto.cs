namespace TheCanalaveLibrary.Core;

/// <summary>
/// Member listing entry for group management and member count displays.
/// <see cref="DisplayName"/> is nullable (user may be deleted — SET NULL on User delete).
/// </summary>
public record GroupMemberDto(
    int UserId,
    string? DisplayName,
    string? AvatarUrl,
    GroupRole Role,
    DateTime DateJoined);
