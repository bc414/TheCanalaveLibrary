namespace TheCanalaveLibrary.Core;

/// <summary>
/// A character ship/platonic pairing for the story write path. Members are referenced
/// by <c>CharacterTagId</c> (must all be in the story's <see cref="StoryCharacterDto"/> list
/// — enforced server-side).
/// </summary>
public sealed class StoryCharacterPairingDto
{
    public CharacterPairingType PairingType { get; init; }
    public TagPriority Priority { get; init; } = TagPriority.Primary;
    public List<int> MemberCharacterTagIds { get; init; } = [];
}
