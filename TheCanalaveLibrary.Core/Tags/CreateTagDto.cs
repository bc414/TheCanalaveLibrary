namespace TheCanalaveLibrary.Core;

/// <summary>DTO for creating a new tag. All fields validated by <see cref="TagValidations"/>.</summary>
public sealed class CreateTagDto
{
    public required string TagName { get; init; }
    public required TagTypeEnum TagTypeId { get; init; }
    public string? Description { get; init; }
    public string? SpriteIdentifier { get; init; }
    public bool IsFanon { get; init; }

    /// <summary>
    /// Whether per-story associations may carry a <c>CustomName</c> (WU-TagFanon; replaced
    /// <c>AllowOCDetails</c>/<c>AllowSettingDetails</c>). Mod judgment, any type — not coerced.
    /// </summary>
    public bool AllowCustomName { get; init; }

    /// <summary>
    /// Optional parent tag ID. Must reference a top-level tag (no parent of its own) of the same
    /// <see cref="TagTypeId"/>. Hierarchy is strictly one level deep.
    /// </summary>
    public int? ParentTagId { get; init; }
}
