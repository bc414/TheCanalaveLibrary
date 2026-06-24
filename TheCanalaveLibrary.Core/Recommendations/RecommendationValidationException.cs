namespace TheCanalaveLibrary.Core;

/// <summary>
/// Thrown by recommendation write operations when tier-2 validation fails.
/// Mirrors <see cref="CommentValidationException"/> and <see cref="ChapterValidationException"/>.
/// </summary>
public class RecommendationValidationException(List<string> errors) : Exception(string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
