namespace TheCanalaveLibrary.Core;

/// <summary>
/// Tier-2 validation for chapter DTOs — static extension methods shared between server and
/// (future) client validation. Mirrors the <c>StoryValidations</c> pattern in Core/Stories/.
/// </summary>
public static class ChapterValidations
{
    /// <summary>
    /// Returns a list of validation error messages, or an empty list when the DTO is valid.
    /// The caller (write service) throws <see cref="ChapterValidationException"/> if the list
    /// is non-empty.
    /// </summary>
    /// <remarks>
    /// Content is validated as non-empty *before* sanitization — an empty raw string will always
    /// produce an empty sanitized string, so checking here is equivalent. Title length is checked
    /// against the 255-character DB constraint.
    /// </remarks>
    public static List<string> CanSave(this CreateChapterDto dto)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(dto.ChapterText))
            errors.Add("Chapter content must not be empty.");

        if (dto.Title is { Length: > 255 })
            errors.Add("Chapter title must be 255 characters or fewer.");

        return errors;
    }

    /// <inheritdoc cref="CanSave(CreateChapterDto)"/>
    public static List<string> CanSave(this UpdateChapterContentDto dto)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(dto.ChapterText))
            errors.Add("Chapter content must not be empty.");

        if (dto.Title is { Length: > 255 })
            errors.Add("Chapter title must be 255 characters or fewer.");

        return errors;
    }
}
