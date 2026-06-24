using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Data Transfer Object for creating a new story. AuthorId is intentionally absent —
/// the server service stamps it from IActiveUserContext.UserId (never trust the client).
/// </summary>
public class CreateStoryDTO : IEditableStoryProperties
{
    public string Title { get; set; } = string.Empty;
    public string? ShortDescription { get; set; } = string.Empty;
    public Rating Rating { get; set; }
    public StoryStatusEnum StoryStatusId { get; set; }
    public string? CoverArtRelativeUrl { get; set; }
    public string? LongDescription { get; set; }
    public StoryStatusEnum PostApprovalStatus { get; set; }
    public List<IStoryTag> StoryTags { get; set; } = new();
}