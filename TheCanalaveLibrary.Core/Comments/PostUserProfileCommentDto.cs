namespace TheCanalaveLibrary.Core;

/// <summary>
/// Payload for posting a new comment (or reply) on a user's profile wall.
/// Mirrors <see cref="PostGroupCommentDto"/> in shape — no spoiler flag, no context-specific extras.
/// <see cref="ProfileUserId"/> is the wall owner (the profile being commented on), not the poster.
/// </summary>
public record PostUserProfileCommentDto(
    int ProfileUserId,
    long? ParentCommentId,
    string CommentText);
