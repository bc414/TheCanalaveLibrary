namespace TheCanalaveLibrary.Core;

/// <summary>
/// Tier-2 validation for recommendation DTOs. Static extension methods, dependency-free.
/// Callers pass sanitized HTML (IHtmlSanitizationService.Sanitize output); validation strips
/// tags and decodes entities before counting characters, matching the approach in
/// <see cref="ChapterText.CountWords"/>.
/// </summary>
public static class RecommendationValidations
{
    /// <summary>
    /// Returns a list of validation error messages, or an empty list when the DTO is valid.
    /// The caller (write service) throws <see cref="RecommendationValidationException"/> if non-empty.
    /// </summary>
    public static List<string> CanSave(this RecommendationSubmitDto dto, string sanitizedHtml)
    {
        var errors = new List<string>();
        int len = RecommendationText.CountPlainTextLength(sanitizedHtml);
        if (len < RecommendationConstants.MinLength)
            errors.Add($"Recommendation must be at least {RecommendationConstants.MinLength} characters (currently {len}).");
        return errors;
    }

    /// <inheritdoc cref="CanSave(RecommendationSubmitDto, string)"/>
    public static List<string> CanSave(this UpdateRecommendationDto dto, string sanitizedHtml)
    {
        var errors = new List<string>();
        int len = RecommendationText.CountPlainTextLength(sanitizedHtml);
        if (len < RecommendationConstants.MinLength)
            errors.Add($"Recommendation must be at least {RecommendationConstants.MinLength} characters (currently {len}).");
        return errors;
    }
}
