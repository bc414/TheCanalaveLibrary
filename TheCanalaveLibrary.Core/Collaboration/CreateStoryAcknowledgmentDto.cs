using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Data required for an author to credit another registered user on one of their stories
/// (WU-StatBadgeProducers). The caller must own <see cref="StoryId"/> — enforced server-side, not
/// here. Unlike <see cref="CreateStoryLineageDto"/>'s self-owned-target auto-approve, a credit is
/// ALWAYS consent-gated (starts <see cref="StoryAcknowledgmentStatus.Pending"/>) — crediting is a
/// claim about someone else's contribution, never something the author can approve on their behalf.
/// </summary>
public class CreateStoryAcknowledgmentDto
{
    [Required]
    public int StoryId { get; set; }

    [Required]
    public int AcknowledgedUserId { get; set; }

    [Required]
    public short AcknowledgmentRoleId { get; set; }
}

public static class CreateStoryAcknowledgmentDtoValidations
{
    /// <summary>Returns validation errors, or an empty list when valid. Existence/ownership/self-
    /// credit checks are the write service's job — this is shape-only.</summary>
    public static List<string> CanSave(this CreateStoryAcknowledgmentDto dto)
    {
        var errors = new List<string>();
        if (dto.StoryId <= 0)
            errors.Add("A story must be selected.");
        if (dto.AcknowledgedUserId <= 0)
            errors.Add("A user must be selected.");
        if (dto.AcknowledgmentRoleId <= 0)
            errors.Add("A role must be selected.");
        return errors;
    }
}
