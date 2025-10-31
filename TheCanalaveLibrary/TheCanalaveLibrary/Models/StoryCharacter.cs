using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class StoryCharacter
{
    public int StoryCharacterId { get; set; }

    public int StoryId { get; set; }

    public int CharacterTagId { get; set; }

    public TagPriority Priority { get; set; }

    public bool IsOc { get; set; }

    public string? OcName { get; set; }

    public string? OcBio { get; set; }

    public virtual Tag CharacterTag { get; set; } = null!;

    public virtual Story Story { get; set; } = null!;

    public virtual ICollection<StoryCharacterRelationship> StoryCharacterRelationships { get; set; } = new List<StoryCharacterRelationship>();
}
