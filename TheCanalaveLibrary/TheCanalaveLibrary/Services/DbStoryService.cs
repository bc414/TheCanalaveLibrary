using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core.DTOs;
using TheCanalaveLibrary.Core.ServiceInterfaces;
using TheCanalaveLibrary.Data;

namespace TheCanalaveLibrary.Services;

public class DbStoryService : IStoryService
{
    private readonly IDbContextFactory<ReadOnlyApplicationDbContext> _dbContextFactory;

    public DbStoryService(IDbContextFactory<ReadOnlyApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<StoryDetailsDTO?> GetStoryByIdAsync(int storyId)
    {
        await using ReadOnlyApplicationDbContext context = await _dbContextFactory.CreateDbContextAsync();
        return await context.Stories.Where(s => s.StoryId == storyId)
            .Select(s => new StoryDetailsDTO
            {
                StoryId = s.StoryId,
                StoryTitle = s.StoryListing.StoryTitle,
                ShortDescription = s.StoryListing.ShortDescription,
                LongDescription = s.StoryDetail.LongDescription,
                WordCount = s.WordCount,
                PublishDate = s.PublishedDate,
                LastUpdatedDate = s.LastUpdatedDate,
                OriginalPublishDate = s.OriginalPublishedDate,
                OriginalLastUpdatedDate = s.OriginalLastUpdatedDate,
                ChapterNames = s.Chapters.Select(c => c.Title).ToList()
            })
            .FirstOrDefaultAsync();
    }
}