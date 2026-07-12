using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Lookup table for <see cref="StoryLineage"/> link flavors (Inspired By / Prequel / Sequel /
/// Companion Piece — seeded in <c>StoryConfigurations.cs</c>). Deliberately a lookup table, not a
/// C# enum, so new flavors can be added without a code deployment.
/// </summary>
public partial class StoryLineageType
{
    [Key]
    public short RelationshipTypeId { get; set; }

    [Required]
    [MaxLength(256)]
    public string TypeName { get; set; } = null!;

    public virtual ICollection<StoryLineage> StoryLineages { get; set; } = new List<StoryLineage>();
}
