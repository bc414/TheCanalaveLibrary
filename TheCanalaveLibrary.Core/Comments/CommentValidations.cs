namespace TheCanalaveLibrary.Core;

/// <summary>
/// Tier-2 validation for comment DTOs — static extension methods shared between server and
/// (future) client validation. Mirrors <see cref="ChapterValidations"/> (WU17).
/// </summary>
public static class CommentValidations
{
    /// <summary>
    /// Returns a list of validation error messages, or an empty list when the DTO is valid.
    /// The caller (write service) throws <see cref="CommentValidationException"/> if the list
    /// is non-empty. Content is validated as non-empty *before* sanitization — an empty raw
    /// string will always produce an empty sanitized string, so checking here is equivalent.
    /// </summary>
    public static List<string> CanSave(this PostChapterCommentDto dto)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(dto.CommentText))
            errors.Add("Comment text must not be empty.");
        return errors;
    }

    /// <inheritdoc cref="CanSave(PostChapterCommentDto)"/>
    public static List<string> CanSave(this PostBlogPostCommentDto dto)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(dto.CommentText))
            errors.Add("Comment text must not be empty.");
        return errors;
    }

    /// <inheritdoc cref="CanSave(PostChapterCommentDto)"/>
    public static List<string> CanSave(this PostGroupCommentDto dto)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(dto.CommentText))
            errors.Add("Comment text must not be empty.");
        return errors;
    }

    /// <inheritdoc cref="CanSave(PostChapterCommentDto)"/>
    public static List<string> CanSave(this UpdateCommentDto dto)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(dto.CommentText))
            errors.Add("Comment text must not be empty.");
        return errors;
    }
}
