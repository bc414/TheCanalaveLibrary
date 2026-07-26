namespace TheCanalaveLibrary.Core;

/// <summary>
/// Per-character association for the story write path. Carries the character tag reference plus
/// the optional overlay pair (WU-TagFanon): <see cref="CustomName"/> (requires <see cref="IsOc"/>;
/// gated server-side by <see cref="Tag.AllowCustomName"/>) and <see cref="Nuance"/> (never gated —
/// a portrayal note is legal on canon and fanon characters alike). A story may carry several rows
/// on the same <see cref="CharacterTagId"/> with distinct custom names.
/// </summary>
public sealed class StoryCharacterDto
{
    public int CharacterTagId { get; init; }
    public TagPriority Priority { get; init; } = TagPriority.Primary;
    public bool IsOc { get; init; }
    public string? CustomName { get; init; }
    public string? Nuance { get; init; }
}
