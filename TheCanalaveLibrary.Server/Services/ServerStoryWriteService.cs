using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

public class ServerStoryWriteService(ReadOnlyApplicationDbContext readDb, ApplicationDbContext writeDb)
    : ServerStoryReadService(readDb), IStoryWriteService
{
    public async Task<int> CreateStoryAsync(CreateStoryDTO newStoryDTO)
    {
        List<string> validationErrors = newStoryDTO.CanSave();
        if (validationErrors.Any())
        {
            throw new StoryValidationException(validationErrors);
        }

        Story newStoryDB = newStoryDTO.ToStory();
        newStoryDB.AuthorId = newStoryDTO.AuthorId;

        // The ToStory() mapper should create the related entities.
        // We need to ensure they are attached to the context.
        writeDb.Attach(newStoryDB.StoryListing);
        writeDb.Attach(newStoryDB.StoryDetail);

        writeDb.Stories.Add(newStoryDB);
        await writeDb.SaveChangesAsync();

        return newStoryDB.StoryId;
    }

    public async Task UpdateStoryAsync(StoryUpdateDTO dto)
    {
        List<string> validationErrors = dto.CanSave();
        if (validationErrors.Any())
        {
            throw new StoryValidationException(validationErrors);
        }

        // Include related data that needs to be updated. This is crucial.
        Story? storyToUpdate = await writeDb.Stories
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

        await writeDb.SaveChangesAsync();
    }
}
