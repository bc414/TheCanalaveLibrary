using System.ComponentModel.DataAnnotations;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// A character-in-story association (Character tags never route through <see cref="StoryTag"/>).
/// Carries the per-story overlay pair (WU-TagFanon): <see cref="CustomName"/> (custom naming —
/// gated by <c>Tag.AllowCustomName</c>, requires <see cref="IsOc"/>) and <see cref="Nuance"/>
/// (per-story portrayal note — never gated, legal on canon and fanon characters alike).
/// A story may hold several custom-named characters of one species —
/// <c>UNIQUE (StoryId, CharacterTagId, CustomName)</c>, nulls not distinct.
/// </summary>
public partial class StoryCharacter
{
    public int StoryCharacterId { get; set; }

    public int StoryId { get; set; }

    public int CharacterTagId { get; set; }

    public TagPriority Priority { get; set; }

    /// <summary>This row is not the tagged character itself but an original character on that
    /// archetype. Legal only where <c>Tag.AllowCustomName</c>. An unnamed OC (true + null
    /// <see cref="CustomName"/>) is legitimate: "features an OC Bulbasaur".</summary>
    public bool IsOc { get; set; }

    [MaxLength(128)]
    public string? CustomName { get; set; }

    [MaxLength(2048)]
    public string? Nuance { get; set; }

    public virtual Tag CharacterTag { get; set; } = null!;

    public virtual Story Story { get; set; } = null!;

    public virtual ICollection<StoryCharacterPairingMember> PairingMemberships { get; set; } = new List<StoryCharacterPairingMember>();
}
