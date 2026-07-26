using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Core;

public partial class Tag
{
    public int TagId { get; set; }

    [Required]
    [MaxLength(100)]
    public string TagName { get; set; } = null!;

    public TagTypeEnum TagTypeId { get; set; }

    public bool IsFanon { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public int? ParentTagId { get; set; }

    [MaxLength(100)]
    public string? SpriteIdentifier { get; set; }

    /// <summary>
    /// Whether per-story associations of this tag may carry a <c>CustomName</c> (a specifically
    /// named instance of the archetype — an OC on <c>Bulbasaur</c>, a custom region on
    /// <c>Original Setting</c>). Mod-set per tag, any type (WU-TagFanon; replaced
    /// <c>AllowOCDetails</c> + <c>AllowSettingDetails</c>). Fanonized tags are specific entities
    /// and get <c>false</c>. Gates custom naming only — <c>Nuance</c> is never gated.
    /// </summary>
    public bool AllowCustomName { get; set; }

    public virtual ICollection<Tag> ChildTags { get; set; } = new List<Tag>();

    public virtual Tag? ParentTag { get; set; }

    public virtual ICollection<StoryCharacter> StoryCharacters { get; set; } = new List<StoryCharacter>();

    public virtual ICollection<StoryTag> StoryTags { get; set; } = new List<StoryTag>();

    public virtual TagType TagType { get; set; } = null!;
}
