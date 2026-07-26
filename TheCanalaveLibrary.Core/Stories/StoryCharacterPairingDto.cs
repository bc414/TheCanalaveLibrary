namespace TheCanalaveLibrary.Core;

/// <summary>
/// A character ship/platonic pairing for the story write path. Members are referenced by
/// <b>index into the story's <see cref="StoryCharacterDto"/> list</b> (WU-TagFanon — tag ids
/// became ambiguous once a story could hold several custom-named characters of one species).
/// Indexes must be distinct and in range; enforced server-side.
/// </summary>
public sealed class StoryCharacterPairingDto
{
    public CharacterPairingType PairingType { get; init; }
    public TagPriority Priority { get; init; } = TagPriority.Primary;
    public List<int> MemberIndexes { get; init; } = [];
}
