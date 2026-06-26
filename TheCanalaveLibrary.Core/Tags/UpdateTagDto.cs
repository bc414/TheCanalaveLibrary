namespace TheCanalaveLibrary.Core;

/// <summary>DTO for editing an existing tag. All fields validated by <see cref="TagValidations"/>.</summary>
public sealed class UpdateTagDto
{
    public required int TagId { get; init; }
    public required string TagName { get; init; }
    public required TagTypeEnum TagTypeId { get; init; }
    public string? Description { get; init; }
    public string? SpriteIdentifier { get; init; }
    public bool IsFanon { get; init; }

    /// <summary>
    /// Only meaningful when <see cref="TagTypeId"/> is <see cref="TagTypeEnum.Character"/>.
    /// Coerced to <c>false</c> for all other types by <see cref="TagValidations"/>.
    /// </summary>
    public bool AllowOCDetails { get; init; }

    /// <summary>
    /// Only meaningful when <see cref="TagTypeId"/> is <see cref="TagTypeEnum.Setting"/>.
    /// Coerced to <c>false</c> for all other types by <see cref="TagValidations"/>.
    /// </summary>
    public bool AllowSettingDetails { get; init; }

    /// <summary>
    /// Optional parent tag ID. Must reference a top-level tag (no parent of its own) of the same
    /// <see cref="TagTypeId"/> and may not be the tag being edited. Hierarchy is strictly one level deep.
    /// </summary>
    public int? ParentTagId { get; init; }
}
