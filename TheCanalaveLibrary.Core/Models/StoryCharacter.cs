using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core.Models;

public partial class StoryCharacter
{
    public int StoryCharacterId { get; set; }

    public int StoryId { get; set; }

    public int CharacterTagId { get; set; }

    public TagPriority Priority { get; set; }

    public bool IsOc { get; set; }

    [MaxLength(128)]
    public string? OcName { get; set; }

    [MaxLength(2048)]
    public string? OcBio { get; set; }

    public virtual Tag CharacterTag { get; set; } = null!;

    public virtual Story Story { get; set; } = null!;

    public virtual ICollection<StoryCharacterRelationship> StoryCharacterRelationships { get; set; } = new List<StoryCharacterRelationship>();
}
