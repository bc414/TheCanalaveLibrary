using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Data required to create a new <see cref="Group"/>.
/// <see cref="AudienceType"/> drives the <c>(AudienceRating, MaxContentRating)</c> pair via
/// <see cref="GroupAudienceTypeMapper.ToRatings"/> — not stored directly.
/// <c>CreatorId</c> is server-stamped from <see cref="IActiveUserContext.UserId"/>; absent here.
/// <see cref="Description"/> is raw HTML from <c>EditorView</c>; sanitized server-side before
/// persisting (layer2-services.md §"User HTML Is Sanitized Once, On Save").
/// </summary>
public class CreateGroupDto
{
    [Required]
    [MaxLength(GroupConstants.MaxGroupNameLength)]
    public string GroupName { get; set; } = string.Empty;

    [MaxLength(GroupConstants.MaxDescriptionLength)]
    public string? Description { get; set; }

    public GroupAudienceType AudienceType { get; set; } = GroupAudienceType.Standard;
}

public static class CreateGroupDtoValidations
{
    /// <summary>Returns validation errors, or an empty list when valid.</summary>
    public static List<string> CanSave(this CreateGroupDto dto)
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
