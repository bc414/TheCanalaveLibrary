using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core.DTOs;
using TheCanalaveLibrary.Core.Models;
using TheCanalaveLibrary.Core.ServiceInterfaces;
using TheCanalaveLibrary.Server.Data;

namespace TheCanalaveLibrary.Server.Services;

/// <summary>
/// Server-side implementation for creating and updating stories.
/// </summary>
public class StoryWriteService : IStoryWriteService
{
    private readonly ApplicationDbContext _context;

    public StoryWriteService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> CreateStoryAsync(StoryCreateDTO dto)
    {
        // Here you would enforce your rich domain model's invariants
        // For simplicity, we are mapping directly.
        Story newStory = new Story
        {
            // AuthorId would come from a user service, not the DTO
            Rating = dto.Rating,
            StoryStatusId = StoryStatusEnum.InProgress, // Default status
            LastUpdatedDate = DateTime.UtcNow,
            PublishedDate = DateTime.UtcNow,
            StoryListing = new StoryListing
            {
                StoryTitle = dto.Title,
                ShortDescription = dto.ShortDescription
            }
        };

        // Logic to find or create tags and associate them would go here.

        _context.Stories.Add(newStory);
        await _context.SaveChangesAsync();

        return newStory.StoryId;
    }

    public async Task UpdateStoryAsync(StoryEditDTO dto)
    {
        // Fetch the existing domain models
        Story? story = await _context.Stories
            .Include(s => s.StoryListing)
            .FirstOrDefaultAsync(s => s.StoryId == dto.StoryId);

        if (story == null)
        {
            // Or throw a custom exception
            return;
        }

        // Apply updates from the DTO to the domain models
        story.Rating = dto.Rating;
        story.StoryListing.StoryTitle = dto.Title;
        story.StoryListing.ShortDescription = dto.ShortDescription;
        story.LastUpdatedDate = DateTime.UtcNow;

        // Logic to update tags would go here (removing old, adding new).

        await _context.SaveChangesAsync();
    }
}