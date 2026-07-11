using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Data required to update an existing <see cref="Series"/>. Owner-only; the write service enforces
/// <c>Series.AuthorId == ActiveUser.UserId</c>. <see cref="Description"/> is sanitized on save.
/// </summary>
public class UpdateSeriesDto
{
    public int SeriesId { get; set; }

    [Required]
    [MaxLength(SeriesConstants.MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(SeriesConstants.MaxDescriptionLength)]
    public string? Description { get; set; }
}

public static class UpdateSeriesDtoValidations
{
    /// <summary>Returns validation errors, or an empty list when valid.</summary>
    public static List<string> CanSave(this UpdateSeriesDto dto)
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
