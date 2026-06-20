namespace TheCanalaveLibrary.Core;

public interface IStoryWriteService
{
    /// <summary>
    /// Creates a new story.
    /// </summary>
    /// <param name="dto">A DTO containing the initial story properties.</param>
    /// <returns>The ID of the newly created story.</returns>
    Task<int> CreateStoryAsync(CreateStoryDTO dto);

    /// <summary>
    /// Updates an existing story's properties.
    /// </summary>
    /// <param name="dto">A DTO containing the story's updated properties.</param>
    Task UpdateStoryAsync(StoryUpdateDTO dto);
}