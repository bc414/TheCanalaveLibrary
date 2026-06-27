namespace TheCanalaveLibrary.Core;

public class StoryCharacterPairingMember
{
    public int StoryCharacterPairingId { get; set; }

    public int StoryCharacterId { get; set; }

    public virtual StoryCharacterPairing Pairing { get; set; } = null!;

    public virtual StoryCharacter StoryCharacter { get; set; } = null!;
}
