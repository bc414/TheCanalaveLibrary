using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Models;

public partial class StoryRelationshipType
{
    public byte RelationshipTypeId { get; set; }

    [Required]
    [MaxLength(256)]
    public string TypeName { get; set; } = null!;

    public virtual ICollection<StoryRelationship> StoryRelationships { get; set; } = new List<StoryRelationship>();
}
