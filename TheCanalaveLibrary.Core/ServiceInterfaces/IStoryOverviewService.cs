using TheCanalaveLibrary.Core.DTOs;

namespace TheCanalaveLibrary.Core.ServiceInterfaces;

public interface IStoryOverviewService
{
    Task<StoryDetailsDTO?> GetStoryByIdAsync(int storyId);
    Task<int> GetRandomNumber();
}