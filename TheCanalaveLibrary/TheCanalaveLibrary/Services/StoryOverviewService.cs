using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core.DTOs;
using TheCanalaveLibrary.Core.ServiceInterfaces;
using TheCanalaveLibrary.Data;
using System.Linq;

namespace TheCanalaveLibrary.Services;

public class StoryOverviewService : IStoryOverviewService
{
    private readonly ReadOnlyApplicationDbContext _dbContext;
    private readonly Random _random = new();

    public StoryOverviewService(ReadOnlyApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<StoryDetailsDTO?> GetStoryByIdAsync(int storyId)
    {
        return await _dbContext.Stories
            .Where(s => s.StoryId == storyId)
            .Select(s => new StoryDetailsDTO
                {
                    StoryId = s.StoryId,
                    StoryTitle = s.StoryListing.StoryTitle,
                    ShortDescription = s.StoryListing.ShortDescription,
                    LongDescription = s.StoryDetail.LongDescription,
                    WordCount = s.WordCount,
                    PublishDate = s.PublishedDate,
                    LastUpdatedDate = s.LastUpdatedDate,
                    AuthorName = s.Author != null ? s.Author.UserName : "Unknown Author",
                    ChapterNames = s.Chapters.OrderBy(c => c.ChapterNumber).Select(c => c.Title).ToList()
                })
            .FirstOrDefaultAsync();
    }

    public Task<int> GetRandomNumber()
    {
        return Task.FromResult(_random.Next(1, 101));
    }
}