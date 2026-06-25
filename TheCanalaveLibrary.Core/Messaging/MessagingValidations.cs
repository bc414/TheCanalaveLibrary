namespace TheCanalaveLibrary.Core;

/// <summary>
/// Tier-3 (server-only) domain validations for messaging operations.
/// Extension methods return a list of error strings; an empty list means valid.
/// </summary>
public static class MessagingValidations
{
    /// <summary>
    /// Validates a <see cref="StartConversationDto"/> before it reaches the write service.
    /// Checks: non-empty subject, non-whitespace message body, and self-message block.
    /// The self-message guard in the write service is the authoritative enforcement point;
    /// this validation is a pre-check so the client receives a structured error rather than
    /// an <see cref="InvalidOperationException"/>.
    /// </summary>
    public static List<string> Validate(this StartConversationDto dto, int senderId)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(dto.Subject))
            errors.Add("Subject is required.");

        if (string.IsNullOrWhiteSpace(dto.MessageHtml))
            errors.Add("Message body must not be empty.");

        if (dto.RecipientUserId == senderId)
            errors.Add("You cannot send a message to yourself.");

        return errors;
    }

    /// <summary>
    /// Validates a raw message body (HTML from <c>EditorView</c>) before appending to a thread.
    /// </summary>
    public static List<string> ValidateMessageBody(string? messageHtml)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(messageHtml))
            errors.Add("Message body must not be empty.");

        return errors;
    }
}
