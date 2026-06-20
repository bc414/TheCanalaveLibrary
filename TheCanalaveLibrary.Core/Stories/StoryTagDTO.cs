using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Core;

public class StoryTagDTO : IStoryTag
{
    public int TagId { get; set; }
    public TagPriority Priority { get; set; }
    public TagTypeEnum TagTypeEnum { get; set; }
}