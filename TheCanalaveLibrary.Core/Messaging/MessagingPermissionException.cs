namespace TheCanalaveLibrary.Core;

/// <summary>
/// Thrown when a messaging write is blocked by the recipient's <c>AllowPrivateMessages</c>
/// privacy setting (e.g. <see cref="SocialInteractionPermission.Nobody"/> or the
/// <see cref="SocialInteractionPermission.Following"/> tier when the sender is not followed).
/// Callers should surface this as a user-visible permission error, not a generic server error.
/// </summary>
public sealed class MessagingPermissionException(string message) : Exception(message)
{
    public MessagingPermissionException()
        : this("This user does not accept private messages.") { }
}
