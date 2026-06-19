namespace TheCanalaveLibrary.Core.Story;

public interface IStoryReadService
{
    Task<StoryDetailsDTO?> GetStoryByIdAsync(int storyId);

    /// <summary>
    /// Gets the data required to edit a story.
    /// </summary>
    /// <param name="storyId">The ID of the story to edit.</param>
    /// <returns>A DTO containing the story's editable properties.</returns>
    Task<StoryUpdateDTO?> GetStoryForEditAsync(int storyId);
}