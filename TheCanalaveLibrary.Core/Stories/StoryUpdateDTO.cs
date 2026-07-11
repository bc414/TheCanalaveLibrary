using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Data Transfer Object for fetching a story's properties for editing and sending the new values back to the server.
/// </summary>
public class StoryUpdateDTO : IEditableStoryProperties
{
    public int StoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ShortDescription { get; set; } = string.Empty;
    public Rating Rating { get; set; }
    public StoryStatusEnum StoryStatusId { get; set; }
    public string? CoverArtRelativeUrl { get; set; }
    public string? LongDescription { get; set; }
    public StoryStatusEnum PostApprovalStatus { get; set; }
    public List<IStoryTag> StoryTags { get; set; } = new();
    public List<StoryCharacterDto> StoryCharacters { get; set; } = new();
    public List<SettingDetailDto> SettingDetails { get; set; } = new();
    public List<StoryCharacterPairingDto> StoryCharacterPairings { get; set; } = new();

    // "Also posted on" links + original dates (Feature 53 reframe, WU38d). Deliberately NOT on
    // IEditableStoryProperties — links are separate rows the write service syncs, not story
    // properties the shared mapper copies.
    public List<StoryExternalLinkEditDto> ExternalLinks { get; set; } = new();
    public DateOnly? OriginalPublishedDate { get; set; }
    public DateOnly? OriginalLastUpdatedDate { get; set; }
}