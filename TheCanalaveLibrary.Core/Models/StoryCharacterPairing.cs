namespace TheCanalaveLibrary.Core;

public class StoryCharacterPairing
{
    public int StoryCharacterPairingId { get; set; }

    public int StoryId { get; set; }

    public CharacterPairingType PairingType { get; set; }

    public TagPriority Priority { get; set; }

    public virtual Story Story { get; set; } = null!;

    public virtual ICollection<StoryCharacterPairingMember> Members { get; set; } = new List<StoryCharacterPairingMember>();
}
