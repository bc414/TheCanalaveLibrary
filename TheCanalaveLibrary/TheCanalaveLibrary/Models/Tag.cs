using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class Tag
{
    public int TagId { get; set; }

    public string TagName { get; set; } = null!;

    public byte TagTypeId { get; set; }

    public bool IsFanon { get; set; }

    public string? Description { get; set; }

    public int? ParentTagId { get; set; }

    public string? SpriteUrl { get; set; }

    public string? AnimatedSpriteUrl { get; set; }

    public bool AllowOcdetails { get; set; }

    public virtual ICollection<Tag> InverseParentTag { get; set; } = new List<Tag>();

    public virtual Tag? ParentTag { get; set; }

    public virtual ICollection<SettingDetail> SettingDetails { get; set; } = new List<SettingDetail>();

    public virtual ICollection<StoryCharacter> StoryCharacters { get; set; } = new List<StoryCharacter>();

    public virtual ICollection<StoryTag> StoryTags { get; set; } = new List<StoryTag>();

    public virtual TagType TagType { get; set; } = null!;
}
