namespace TheCanalaveLibrary.Core;

/// <summary>
/// Thrown by comment write operations when tier-2 validation fails.
/// Mirrors <see cref="ChapterValidationException"/> (WU17).
/// </summary>
public class CommentValidationException(List<string> errors)
    : CanalaveValidationException(string.Join("; ", errors), errors);
