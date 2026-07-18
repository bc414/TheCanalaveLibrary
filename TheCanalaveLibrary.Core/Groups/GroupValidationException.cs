namespace TheCanalaveLibrary.Core;

/// <summary>
/// Thrown by <c>ServerGroupWriteService</c> when a group create/edit/folder operation fails
/// input validation (name/description length, folder MaxRating cap, etc.).
/// Mirrors <see cref="BlogPostValidationException"/> / <see cref="CommentValidationException"/>.
/// </summary>
public class GroupValidationException(IReadOnlyList<string> errors)
    : CanalaveValidationException(string.Join(" ", errors), errors);
