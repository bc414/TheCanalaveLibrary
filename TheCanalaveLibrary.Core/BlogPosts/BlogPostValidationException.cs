namespace TheCanalaveLibrary.Core;

/// <summary>
/// Thrown by blog post write operations when tier-2 validation fails.
/// Mirrors <see cref="CommentValidationException"/> (WU19) and <see cref="ChapterValidationException"/> (WU17).
/// </summary>
public class BlogPostValidationException(List<string> errors) : Exception(string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
