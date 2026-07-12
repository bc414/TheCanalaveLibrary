namespace TheCanalaveLibrary.Core;

/// <summary>
/// A directional link declaring that one story descends from, precedes, follows, or accompanies
/// another — e.g. "Inspired By", "Prequel", "Sequel", "Companion Piece" (Feature 10, WU42).
/// One-way: the absence of a reverse row means "don't show on the target story." A link where
/// <see cref="SourceStory"/> and <see cref="TargetStory"/> are owned by different authors requires
/// the target author's approval (<see cref="StoryLineageStatus.Pending"/> until then); a
/// self-owned link is created already <see cref="StoryLineageStatus.Approved"/>.
/// </summary>
public partial class StoryLineage
{
    public int SourceStoryId { get; set; }

    public int TargetStoryId { get; set; }

    public short RelationshipTypeId { get; set; }

    public StoryLineageStatus StatusId { get; set; }

    public DateTime DateCreated { get; set; }

    public virtual StoryLineageType RelationshipType { get; set; } = null!;

    public virtual Story SourceStory { get; set; } = null!;

    public virtual Story TargetStory { get; set; } = null!;
}
