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

    [MaxLength(512)]
    public string? Description { get; set; }

    public int? ParentTagId { get; set; }

    [MaxLength(50)]
    public string? SpriteIdentifier { get; set; }

    public bool AllowOCDetails { get; set; }

    public bool AllowSettingDetails { get; set; }

    public virtual ICollection<Tag> ChildTags { get; set; } = new List<Tag>();

    public virtual Tag? ParentTag { get; set; }

    public virtual ICollection<SettingDetail> SettingDetails { get; set; } = new List<SettingDetail>();

    public virtual ICollection<StoryCharacter> StoryCharacters { get; set; } = new List<StoryCharacter>();

    public virtual ICollection<StoryTag> StoryTags { get; set; } = new List<StoryTag>();

    public virtual TagType TagType { get; set; } = null!;
}
