using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class StoryRelationshipType
{
    public byte RelationshipTypeId { get; set; }

    public string TypeName { get; set; } = null!;

    public virtual ICollection<StoryRelationship> StoryRelationships { get; set; } = new List<StoryRelationship>();
}
