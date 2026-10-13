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
    /// is non-empty. Content is validated *before* sanitization, through
    /// <see cref="RichTextContent.IsBlank"/>: an emptied editor's <c>&lt;p&gt;&lt;br&gt;&lt;/p&gt;</c> is blank too.
    /// </summary>
    public static List<string> CanSave(this PostChapterCommentDto dto)
    {
        var errors = new List<string>();
        if (RichTextContent.IsBlank(dto.CommentText))
            errors.Add("Comment text must not be empty.");
        return errors;
    }

    /// <inheritdoc cref="CanSave(PostChapterCommentDto)"/>
    public static List<string> CanSave(this PostBlogPostCommentDto dto)
    {
        var errors = new List<string>();
        if (RichTextContent.IsBlank(dto.CommentText))
            errors.Add("Comment text must not be empty.");
        return errors;
    }

    /// <inheritdoc cref="CanSave(PostChapterCommentDto)"/>
    public static List<string> CanSave(this PostGroupCommentDto dto)
    {
        var errors = new List<string>();
        if (RichTextContent.IsBlank(dto.CommentText))
            errors.Add("Comment text must not be empty.");
        return errors;
    }

    /// <inheritdoc cref="CanSave(PostChapterCommentDto)"/>
    public static List<string> CanSave(this PostUserProfileCommentDto dto)
    {
        var errors = new List<string>();
        if (RichTextContent.IsBlank(dto.CommentText))
            errors.Add("Comment text must not be empty.");
        return errors;
    }

    /// <inheritdoc cref="CanSave(PostChapterCommentDto)"/>
    public static List<string> CanSave(this UpdateCommentDto dto)
    {
        var errors = new List<string>();
        if (RichTextContent.IsBlank(dto.CommentText))
            errors.Add("Comment text must not be empty.");
        return errors;
    }
}
