using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core.Models;
using TheCanalaveLibrary.Core.ServiceInterfaces;
using TheCanalaveLibrary.Core.Story;
using TheCanalaveLibrary.Data;

namespace TheCanalaveLibrary.Services;

public class DbStoryWriteService : IStoryWriteService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public DbStoryWriteService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<int> CreateStoryAsync(CreateStoryDTO newStoryDTO)
    {
        List<string> validationErrors = newStoryDTO.CanSave();
        if (validationErrors.Any())
        {
            throw new StoryValidationException(validationErrors);
        }

        await using ApplicationDbContext context = await _dbContextFactory.CreateDbContextAsync();

        Story newStoryDB = newStoryDTO.ToStory();
        newStoryDB.AuthorId = newStoryDTO.AuthorId;

        // The ToStory() mapper should create the related entities.
        // We need to ensure they are attached to the context.
        context.Attach(newStoryDB.StoryListing);
        context.Attach(newStoryDB.StoryDetail);

        context.Stories.Add(newStoryDB);
        await context.SaveChangesAsync();

        return newStoryDB.StoryId;
    }

    public async Task UpdateStoryAsync(StoryUpdateDTO dto)
    {
        List<string> validationErrors = dto.CanSave();
        if (validationErrors.Any())
        {
            throw new StoryValidationException(validationErrors);
        }

        await using ApplicationDbContext context = await _dbContextFactory.CreateDbContextAsync();

        // Include related data that needs to be updated. This is crucial.
        Story? storyToUpdate = await context.Stories
            .Include(s => s.StoryListing)
            .Include(s => s.StoryDetail)
            .Include(s => s.StoryTags)
            .FirstOrDefaultAsync(s => s.StoryId == dto.StoryId);

        if (storyToUpdate == null)
        {
            // Throwing an exception is clearer than returning an error string.
            // The API controller can translate this to a 404 Not Found.
            throw new KeyNotFoundException($"Story with ID {dto.StoryId} not found.");
        }

        storyToUpdate.UpdateStoryEditableProperties(dto);

        await context.SaveChangesAsync();
    }
}