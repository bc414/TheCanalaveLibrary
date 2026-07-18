namespace TheCanalaveLibrary.Core;

/// <summary>
/// Thrown by recommendation write operations when tier-2 validation fails.
/// Mirrors <see cref="CommentValidationException"/> and <see cref="ChapterValidationException"/>.
/// </summary>
public class RecommendationValidationException(List<string> errors)
    : CanalaveValidationException(string.Join("; ", errors), errors);
