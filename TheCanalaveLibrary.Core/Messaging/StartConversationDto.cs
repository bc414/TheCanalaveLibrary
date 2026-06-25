namespace TheCanalaveLibrary.Core;

/// <summary>
/// Input DTO for starting a new 1-on-1 conversation.
/// <para>
/// <see cref="MessageHtml"/> is the raw HTML pulled from <c>EditorView.GetHtmlAsync()</c>;
/// the write service sanitizes it before persisting (sanitize-once-on-save).
/// </para>
/// <para>
/// Multiple conversations between the same two users are allowed — each has a distinct
/// <see cref="Subject"/>. There is no "existing conversation" merge step.
/// </para>
/// </summary>
public record StartConversationDto(
    int RecipientUserId,
    string Subject,
    string MessageHtml);
