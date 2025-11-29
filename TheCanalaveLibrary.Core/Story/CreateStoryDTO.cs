using TheCanalaveLibrary.Core.Models;
using TheCanalaveLibrary.Core.Tags;

namespace TheCanalaveLibrary.Core.Story;

/// <summary>
/// Data Transfer Object for creating a new story. This can be expanded
/// to be identical to an "Update" DTO if the fields are the same.
/// </summary>
public class CreateStoryDTO : IEditableStoryProperties
{
    public int AuthorId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ShortDescription { get; set; } = string.Empty;
    public Rating Rating { get; set; }
    public StoryStatusEnum StoryStatusId { get; set; }
    public string? CoverArtRelativeUrl { get; set; }
    public string? LongDescription { get; set; }
    public StoryStatusEnum PostApprovalStatus { get; set; }
    public List<IStoryTag> StoryTags { get; set; } = new();
}