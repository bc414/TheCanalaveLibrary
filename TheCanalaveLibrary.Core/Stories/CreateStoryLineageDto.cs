using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Data required to request a new <see cref="StoryLineage"/> link (Feature 10, WU42). The caller
/// must own <see cref="SourceStoryId"/> — enforced server-side, not here. When the caller also owns
/// <see cref="TargetStoryId"/> the link is created already <see cref="StoryLineageStatus.Approved"/>;
/// otherwise it starts <see cref="StoryLineageStatus.Pending"/> and the target author is notified.
/// </summary>
public class CreateStoryLineageDto
{
    [Required]
    public int SourceStoryId { get; set; }

    [Required]
    public int TargetStoryId { get; set; }

    [Required]
    public short TypeId { get; set; }
}

public static class CreateStoryLineageDtoValidations
{
    /// <summary>Returns validation errors, or an empty list when valid. Existence checks
    /// (target story/type actually exist) are the write service's job — this is shape-only.</summary>
    public static List<string> CanSave(this CreateStoryLineageDto dto)
    {
        var errors = new List<string>();
        if (dto.SourceStoryId <= 0)
            errors.Add("A source story must be selected.");
        if (dto.TargetStoryId <= 0)
            errors.Add("A target story must be selected.");
        if (dto.SourceStoryId > 0 && dto.SourceStoryId == dto.TargetStoryId)
            errors.Add("A story cannot have a lineage link to itself.");
        if (dto.TypeId <= 0)
            errors.Add("A lineage type must be selected.");
        return errors;
    }
}
