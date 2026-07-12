namespace TheCanalaveLibrary.Core;

/// <summary>
/// An option row within <see cref="PollEditDto"/>. <see cref="PollOptionId"/> null = new option;
/// non-null = retain the existing option (preserving its votes) with possibly-updated text.
/// Final SortOrder is the row's index in <see cref="PollEditDto.Options"/>.
/// </summary>
public record PollOptionEditDto(int? PollOptionId, string Text);

/// <summary>
/// Create/update payload for a poll (both site and blog-post kinds — the target is fixed by the
/// service method, never by the DTO). On update with existing votes, the three config fields
/// must match the stored values (config lock — service-enforced) and <see cref="DateOpened"/>
/// must be unchanged.
/// </summary>
public class PollEditDto
{
    public string PollName { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Null on create = open immediately. A future value schedules the open.</summary>
    public DateTime? DateOpened { get; set; }

    /// <summary>Null = indefinite (open until manually closed).</summary>
    public DateTime? DateClosed { get; set; }

    public bool AllowMultiple { get; set; }
    public PollResultsVisibility ResultsVisibility { get; set; }
    public PollAnonymityMode AnonymityMode { get; set; }

    public List<PollOptionEditDto> Options { get; set; } = [];

    /// <summary>
    /// Tier-2 validation (mirrors <c>BlogPostValidations</c>' CanSave pattern).
    /// Returns an empty list when saveable.
    /// </summary>
    public List<string> CanSave()
    {
        List<string> errors = [];

        if (string.IsNullOrWhiteSpace(PollName))
            errors.Add("A poll needs a name.");
        else if (PollName.Trim().Length > 256)
            errors.Add("Poll name must be 256 characters or fewer.");

        if (Description is { Length: > 2048 })
            errors.Add("Description must be 2048 characters or fewer.");

        List<string> texts = Options
            .Select(o => o.Text?.Trim() ?? string.Empty)
            .ToList();

        if (texts.Count < 2)
            errors.Add("A poll needs at least two options.");
        if (texts.Any(string.IsNullOrWhiteSpace))
            errors.Add("Options cannot be blank.");
        if (texts.Any(t => t.Length > 2048))
            errors.Add("Options must be 2048 characters or fewer.");
        if (texts.Where(t => t.Length > 0).Distinct(StringComparer.Ordinal).Count()
            != texts.Count(t => t.Length > 0))
            errors.Add("Options must be unique within the poll.");

        if (DateOpened is DateTime opened && DateClosed is DateTime closed && closed <= opened)
            errors.Add("The close date must be after the open date.");

        return errors;
    }
}
