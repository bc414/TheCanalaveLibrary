using TheCanalaveLibrary.Core.DTOs;

namespace TheCanalaveLibrary.Core.ServiceInterfaces;

public interface IStoryService
{
    Task<StoryDetailsDTO?> GetStoryByIdAsync(int storyId);
}