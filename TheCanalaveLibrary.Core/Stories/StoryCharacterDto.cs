namespace TheCanalaveLibrary.Core;

/// <summary>
/// Per-character association for the story write path. Carries the character tag reference
/// plus the optional OC overlay (gated server-side by <see cref="Tag.AllowOCDetails"/>).
/// </summary>
public sealed class StoryCharacterDto
{
    public int CharacterTagId { get; init; }
    public TagPriority Priority { get; init; } = TagPriority.Primary;
    public bool IsOc { get; init; }
    public string? OcName { get; init; }
    public string? OcBio { get; init; }
}
