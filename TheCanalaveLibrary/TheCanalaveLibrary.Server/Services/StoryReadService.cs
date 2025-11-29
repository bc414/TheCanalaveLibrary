using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core.DTOs;
using TheCanalaveLibrary.Core.ServiceInterfaces;
using TheCanalaveLibrary.Server.Data;

namespace TheCanalaveLibrary.Server.Services;

/// <summary>
/// Server-side implementation for reading story data.
/// </summary>
public class StoryReadService : IStoryReadService
{
    private readonly ApplicationDbContext _context;

    public StoryReadService(ApplicationDbContext context)
    {
        _context = context;
    }

    // This is a placeholder implementation. You would build out the real DTO projection here.
    public async Task<StoryDetailsDTO?> GetStoryByIdAsync(int storyId)
    {
        return await _context.Stories
            .Where(s => s.StoryId == storyId)
            .Select(s => new StoryDetailsDTO { Title = s.StoryListing.StoryTitle }) // Example mapping
            .FirstOrDefaultAsync();
    }

    public async Task<StoryEditDTO?> GetStoryForEditAsync(int storyId)
    {
        StoryEditDTO? story = await _context.Stories
            .Where(s => s.StoryId == storyId)
            .Select(s => new StoryEditDTO
            {
                StoryId = s.StoryId,
                Title = s.StoryListing.StoryTitle,
                ShortDescription = s.StoryListing.ShortDescription ?? string.Empty,
                Rating = s.Rating,
                Tags = s.StoryTags.Select(st => st.Tag.Name).ToList()
            })
            .FirstOrDefaultAsync();

        return story;
    }
}