namespace TheCanalaveLibrary.Core.DTOs;

/// <summary>
/// Contains all the details needed to display a story's summary page. Too dense for search results, but good for
/// the story's main page.
/// </summary>
public class StoryDetailsDTO
{
    public int StoryId { get; set; }
    public string? StoryTitle { get; set; }
    public string? ShortDescription { get; set; }
    public string? LongDescription { get; set; }
    public int WordCount { get; set; }
    public DateTime PublishDate { get; set; }
    public DateTime LastUpdatedDate { get; set; }
    public DateOnly? OriginalPublishDate { get; set; }
    public DateOnly? OriginalLastUpdatedDate { get; set; }
    public string? AuthorName { get; set; }

    public List<string> ChapterNames { get; set; } = [];
}