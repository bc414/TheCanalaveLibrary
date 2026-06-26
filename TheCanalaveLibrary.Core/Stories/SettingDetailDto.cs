namespace TheCanalaveLibrary.Core;

/// <summary>
/// Optional custom-detail overlay for a Setting tag associated with the story.
/// A <see cref="SettingDetailDto"/> is only legal when the backing
/// <see cref="Tag.AllowSettingDetails"/> flag is true (enforced server-side).
/// </summary>
public sealed class SettingDetailDto
{
    public int BaseTagId { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
}
