namespace TheCanalaveLibrary.Core;

/// <summary>
/// Slim identity of a conversation participant — the other person in the thread.
/// Avatar URL is copied verbatim from <c>User.ProfilePictureRelativeUrl</c> (or a service-chosen
/// default), never resolved through <c>ISpriteReadService</c>. Request-scoped; do not cache.
/// </summary>
public record MessagingParticipantDto(
    int UserId,
    string Username,
    string AvatarUrl);
