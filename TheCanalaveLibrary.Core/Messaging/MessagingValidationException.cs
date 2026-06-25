namespace TheCanalaveLibrary.Core;

/// <summary>
/// Thrown when a messaging operation fails domain-level validation (Tier 3 server-only).
/// Mirrors <c>CommentValidationException</c> — a list of human-readable error strings.
/// </summary>
public class MessagingValidationException(List<string> errors)
    : Exception(string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
