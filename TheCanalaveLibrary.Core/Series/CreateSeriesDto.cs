using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Data required to create a new <see cref="Series"/>. <c>AuthorId</c> is server-stamped from
/// <see cref="IActiveUserContext.UserId"/>; absent here. <see cref="Description"/> is sanitized
/// server-side before persisting (layer2-services.md §"User HTML Is Sanitized Once, On Save").
/// </summary>
public class CreateSeriesDto
{
    [Required]
    [MaxLength(SeriesConstants.MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(SeriesConstants.MaxDescriptionLength)]
    public string? Description { get; set; }
}

public static class CreateSeriesDtoValidations
{
    /// <summary>Returns validation errors, or an empty list when valid.</summary>
    public static List<string> CanSave(this CreateSeriesDto dto)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(dto.Name))
            errors.Add("Series name must not be empty.");
        else if (dto.Name.Trim().Length > SeriesConstants.MaxNameLength)
            errors.Add($"Series name must be {SeriesConstants.MaxNameLength} characters or fewer.");
        if (dto.Description is { Length: > SeriesConstants.MaxDescriptionLength })
            errors.Add($"Description must be {SeriesConstants.MaxDescriptionLength} characters or fewer.");
        return errors;
    }
}
