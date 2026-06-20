using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Used for validating the tags attached to a story. The priority is something specific to the relationship
/// between a tag and a story.
/// </summary>
public interface IStoryTag
{
    public int TagId { get; set; }

    public TagPriority Priority { get; set; }
    
    public TagTypeEnum TagTypeEnum { get; }
}