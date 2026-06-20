using System.ComponentModel.DataAnnotations.Schema;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Core;

public partial class StoryTag : IStoryTag
{
    public int StoryId { get; set; }

    public int TagId { get; set; }

    public TagPriority Priority { get; set; }
    

    public virtual Story Story { get; set; } = null!;

    public virtual Tag Tag { get; set; } = null!;

    [NotMapped] TagTypeEnum IStoryTag.TagTypeEnum => Tag.TagTypeId;
}
