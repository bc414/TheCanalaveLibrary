namespace TheCanalaveLibrary.Core;

/// <summary>
/// Tier-2 validation for blog post DTOs — static extension methods shared between server and
/// (future) client validation. Mirrors <see cref="CommentValidations"/> (WU19).
/// Content is validated <em>before</em> HTML sanitization, through <see cref="RichTextContent.IsBlank"/>:
/// an emptied editor's <c>&lt;p&gt;&lt;br&gt;&lt;/p&gt;</c> is blank too.
/// </summary>
public static class BlogPostValidations
{
    /// <summary>
    /// Returns a list of validation error messages, or an empty list when the DTO is valid.
    /// The caller (write service) throws <see cref="BlogPostValidationException"/> if the list
    /// is non-empty.
    /// </summary>
    public static List<string> CanSave(this CreateProfileBlogPostDto dto)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(dto.Title))
            errors.Add("Title must not be empty.");
        else if (dto.Title.Length > 256)
            errors.Add("Title must be 256 characters or fewer.");
        if (RichTextContent.IsBlank(dto.Content))
            errors.Add("Content must not be empty.");
        return errors;
    }

    /// <inheritdoc cref="CanSave(CreateProfileBlogPostDto)"/>
    public static List<string> CanSave(this UpdateBlogPostDto dto)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(dto.Title))
            errors.Add("Title must not be empty.");
        else if (dto.Title.Length > 256)
            errors.Add("Title must be 256 characters or fewer.");
        if (RichTextContent.IsBlank(dto.Content))
            errors.Add("Content must not be empty.");
        return errors;
    }
}
