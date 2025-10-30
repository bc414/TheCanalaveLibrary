using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class StoryCharacterRelationship
{
    public int StoryCharacterRelationshipId { get; set; }

    public int StoryId { get; set; }

    public byte RelationshipType { get; set; }

    public byte Priority { get; set; }

    public virtual Story Story { get; set; } = null!;

    public virtual ICollection<StoryCharacter> StoryCharacters { get; set; } = new List<StoryCharacter>();
}
