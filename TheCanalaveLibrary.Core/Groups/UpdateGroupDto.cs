using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Data required to update an existing <see cref="Group"/>. Admin-only; the write service enforces
/// the ownership check. <see cref="AudienceType"/> drives the rating pair via
/// <see cref="GroupAudienceTypeMapper.ToRatings"/>. <see cref="Description"/> is sanitized on save.
/// </summary>
public class UpdateGroupDto
{
    public int GroupId { get; set; }

    [Required]
    [MaxLength(GroupConstants.MaxGroupNameLength)]
    public string GroupName { get; set; } = string.Empty;

    [MaxLength(GroupConstants.MaxDescriptionLength)]
    public string? Description { get; set; }

    public GroupAudienceType AudienceType { get; set; }
}

public static class UpdateGroupDtoValidations
{
    /// <summary>Returns validation errors, or an empty list when valid.</summary>
    public static List<string> CanSave(this UpdateGroupDto dto)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(dto.GroupName))
            errors.Add("Group name must not be empty.");
        else if (dto.GroupName.Trim().Length > GroupConstants.MaxGroupNameLength)
            errors.Add($"Group name must be {GroupConstants.MaxGroupNameLength} characters or fewer.");
        if (dto.Description is { Length: > GroupConstants.MaxDescriptionLength })
            errors.Add($"Description must be {GroupConstants.MaxDescriptionLength} characters or fewer.");
        return errors;
    }
}
