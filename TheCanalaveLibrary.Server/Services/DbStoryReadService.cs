using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core.ServiceInterfaces;
using TheCanalaveLibrary.Core.Story;
using TheCanalaveLibrary.Server.Data;

namespace TheCanalaveLibrary.Server.Services;

public class DbStoryReadService : IStoryReadService
{
    private readonly IDbContextFactory<ReadOnlyApplicationDbContext> _dbContextFactory;

    public DbStoryReadService(IDbContextFactory<ReadOnlyApplicationDbContext> dbContextFactory)
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
                StoryTitle = s.StoryListing != null ? s.StoryListing.StoryTitle : string.Empty,
                ShortDescription = s.StoryListing != null ? s.StoryListing.ShortDescription ?? string.Empty : string.Empty,
                LongDescription = s.StoryDetail.LongDescription ?? string.Empty,
                WordCount = s.WordCount,
                PublishDate = s.PublishedDate,
                LastUpdatedDate = s.LastUpdatedDate,
                OriginalPublishDate = s.OriginalPublishedDate,
                OriginalLastUpdatedDate = s.OriginalLastUpdatedDate,
                ChapterNames = s.Chapters.Select(c => c.Title).ToList(),
                AuthorName = s.Author != null ? s.Author.UserName : "Unknown"
            })
            .FirstOrDefaultAsync();
    }

    public async Task<StoryUpdateDTO?> GetStoryForEditAsync(int storyId)
    {
        await using ReadOnlyApplicationDbContext context = await _dbContextFactory.CreateDbContextAsync();
        return await context.Stories // Using a direct projection for optimal query generation
            .Where(s => s.StoryId == storyId)
            .Select(s => new StoryUpdateDTO
            {
                StoryId = s.StoryId,
                Title = s.StoryListing != null ? s.StoryListing.StoryTitle : string.Empty,
                ShortDescription = s.StoryListing != null ? s.StoryListing.ShortDescription : null,
                Rating = s.Rating,
                StoryStatusId = s.StoryStatusId,
                CoverArtRelativeUrl = s.StoryListing != null ? s.StoryListing.CoverArtRelativeUrl : null,
                LongDescription = s.StoryDetail != null ? s.StoryDetail.LongDescription : null,
                PostApprovalStatus = s.StoryDetail != null ? s.StoryDetail.PostApprovalStatus : default,
                StoryTags = s.StoryTags.Select(st => new StoryTagDTO { TagId = st.TagId, Priority = st.Priority, TagTypeEnum = st.Tag.TagTypeId }).ToList<IStoryTag>()
            })
            .FirstOrDefaultAsync();
    }
}