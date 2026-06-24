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
    /// <param name="dto">The DTO to validate.</param>
    /// <param name="storyRating">
    /// When provided, enforces the rating floor (version rating must be ≥ story rating).
    /// Pass <c>null</c> when the story rating is not yet available.
    /// </param>
    /// <param name="isPrimary">
    /// When <c>true</c>, additionally enforces the primary invariant: the version's effective
    /// rating must equal the story rating (null/inherit passes; an explicit override must equal).
    /// Only applies when <paramref name="storyRating"/> is also provided.
    /// </param>
    /// <remarks>
    /// Content is validated as non-empty *before* sanitization — an empty raw string will always
    /// produce an empty sanitized string, so checking here is equivalent. Title length is checked
    /// against the 255-character DB constraint.
    /// </remarks>
    public static List<string> CanSave(this CreateChapterDto dto,
        Rating? storyRating = null,
        bool isPrimary = false)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(dto.ChapterText))
            errors.Add("Chapter content must not be empty.");

        if (dto.Title is { Length: > 255 })
            errors.Add("Chapter title must be 255 characters or fewer.");

        if (storyRating.HasValue && dto.Rating.HasValue)
        {
            if (dto.Rating.Value < storyRating.Value)
                errors.Add($"A version can't be rated below the story's rating ({storyRating.Value}).");

            if (isPrimary && dto.Rating.Value != storyRating.Value)
                errors.Add($"To set this as the default version, its rating must match the story's rating ({storyRating.Value}). Raise the story's rating first, or leave this field as inherit.");
        }

        return errors;
    }

    /// <inheritdoc cref="CanSave(CreateChapterDto, Rating?, bool)"/>
    public static List<string> CanSave(this UpdateChapterContentDto dto,
        Rating? storyRating = null,
        bool isPrimary = false)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(dto.ChapterText))
            errors.Add("Chapter content must not be empty.");

        if (dto.Title is { Length: > 255 })
            errors.Add("Chapter title must be 255 characters or fewer.");

        if (storyRating.HasValue && dto.Rating.HasValue)
        {
            if (dto.Rating.Value < storyRating.Value)
                errors.Add($"A version can't be rated below the story's rating ({storyRating.Value}).");

            if (isPrimary && dto.Rating.Value != storyRating.Value)
                errors.Add($"To set this as the default version, its rating must match the story's rating ({storyRating.Value}). Raise the story's rating first, or leave this field as inherit.");
        }

        return errors;
    }
}
