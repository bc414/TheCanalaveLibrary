namespace TheCanalaveLibrary.Core;

/// <summary>
/// Tuning constants for the Messaging feature (Feature 49).
/// </summary>
public static class MessagingConstants
{
    /// <summary>Number of messages per page in a conversation thread.</summary>
    public const int ThreadPageSize = 20;

    /// <summary>
    /// Longest conversation subject, in characters — the <c>conversations.subject</c> column cap
    /// (<see cref="Conversation.Subject"/>'s <c>[MaxLength]</c> reads this constant, so the two cannot
    /// drift). <see cref="MessagingValidations"/> enforces it on the trimmed subject, so an oversized
    /// subject is a 400 with a message rather than a database 500 (WU-NotificationCorrectness,
    /// 2026-10-12). The number predates that WU; it picks no new bound.
    /// </summary>
    public const int MaxSubjectLength = 2048;
}
