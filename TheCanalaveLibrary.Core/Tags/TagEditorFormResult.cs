namespace TheCanalaveLibrary.Core;

/// <summary>
/// Emitted by <c>TagEditorForm</c> on valid submit. The page dispatcher maps this to
/// <see cref="CreateTagDto"/> or <see cref="UpdateTagDto"/> before calling the write service.
/// </summary>
public sealed class TagEditorFormResult
{
    public required string TagName { get; init; }
    public required TagTypeEnum TagTypeId { get; init; }
    public string? Description { get; init; }
    public string? SpriteIdentifier { get; init; }
    public bool IsFanon { get; init; }
    public bool AllowOCDetails { get; init; }
    public bool AllowSettingDetails { get; init; }
    public int? ParentTagId { get; init; }
}
