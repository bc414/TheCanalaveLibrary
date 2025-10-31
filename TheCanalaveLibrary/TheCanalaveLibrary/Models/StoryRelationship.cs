using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

/// <summary>
/// A relationship between two stories, such as InspiredBy or Sequel
/// </summary>
public partial class StoryRelationship
{
    public int SourceStoryId { get; set; }

    public int TargetStoryId { get; set; }

    public byte RelationshipTypeId { get; set; }

    public StoryRelationshipStatus StatusId { get; set; }

    public DateTime DateCreated { get; set; }

    public virtual StoryRelationshipType RelationshipType { get; set; } = null!;

    public virtual Story SourceStory { get; set; } = null!;

    public virtual Story TargetStory { get; set; } = null!;
}
