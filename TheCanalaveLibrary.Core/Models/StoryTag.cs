namespace TheCanalaveLibrary.Core.Models;

public partial class StoryTag
{
    public int StoryId { get; set; }

    public int TagId { get; set; }

    public TagPriority Priority { get; set; }

    public virtual Story Story { get; set; } = null!;

    public virtual Tag Tag { get; set; } = null!;
}
