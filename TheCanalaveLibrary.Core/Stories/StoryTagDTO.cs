using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Core;

public class StoryTagDTO : IStoryTag
{
    public int TagId { get; set; }
    public TagPriority Priority { get; set; }
    public TagTypeEnum TagTypeEnum { get; set; }

    /// <summary>Per-story named instance of the archetype (gated by <c>Tag.AllowCustomName</c>).</summary>
    public string? CustomName { get; set; }

    /// <summary>Per-story note on the tag — never gated, any type (WU-TagFanon).</summary>
    public string? Nuance { get; set; }
}
