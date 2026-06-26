using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Defines a common set of properties for story-like objects
/// that can be validated and mapped. These will be used for the write-path
/// and must contain all of a story's metadata that a user can edit.
/// </summary>
public interface IEditableStoryProperties
{
    public string Title { get; set; }
    public string? ShortDescription { get; set; }
    public Rating Rating { get; set; }
    
    public StoryStatusEnum StoryStatusId { get; set; }
    
    public string? CoverArtRelativeUrl { get; set; }
    
    public string? LongDescription { get; set; }
    
    public StoryStatusEnum PostApprovalStatus { get; set; }
    
    /// <summary>Genre, ContentWarning, CrossoverFandom, and Setting flat associations only.
    /// Character type is stored separately in <see cref="StoryCharacters"/>.</summary>
    public List<IStoryTag> StoryTags { get; set; }

    public List<StoryCharacterDto> StoryCharacters { get; set; }
    public List<SettingDetailDto> SettingDetails { get; set; }
    public List<StoryCharacterPairingDto> StoryCharacterPairings { get; set; }
}