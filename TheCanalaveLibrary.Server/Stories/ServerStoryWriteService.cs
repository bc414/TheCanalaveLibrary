using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

public class ServerStoryWriteService(
    ReadOnlyApplicationDbContext readDb,
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser,
    ISpriteReadService spriteReadService)
    : ServerStoryReadService(readDb, activeUser, spriteReadService), IStoryWriteService
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
        // Slug is server-only and never client-editable (not on CreateStoryDTO at all) — settled WU12.
        newStoryDB.StoryDetail.Slug = await GenerateUniqueSlugAsync(newStoryDTO.Title);

        // Story.StoryListing/StoryDetail are reachable navigations on a connected object graph — Add()
        // tracks the whole graph as Added, so no separate Attach() is needed. The original code's
        // Attach() calls were a real bug, not just redundant: Attach marks an entity Unchanged, which
        // would make SaveChangesAsync skip inserting the listing/detail rows entirely (fixed WU12,
        // alongside the ToStory() NRE this depended on — see StoryMappers.cs).
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

    // Tier-3 (cross-row, server set-based) uniqueness check per spec §3.7 — the unique-filtered index
    // on StoryDetail.Slug is the backstop, not the primary mechanism. Settled WU12. The pure text
    // transform lives in Core (StorySlug.Slugify, unit-tested there); only the DB scan stays here.
    private async Task<string> GenerateUniqueSlugAsync(string title)
    {
        string baseSlug = StorySlug.Slugify(title);
        if (baseSlug.Length == 0) baseSlug = "story";

        HashSet<string> existingSlugs = (await writeDb.StoryDetails
                .Where(d => d.Slug != null && (d.Slug == baseSlug || d.Slug.StartsWith(baseSlug + "-")))
                .Select(d => d.Slug!)
                .ToListAsync())
            .ToHashSet();

        if (!existingSlugs.Contains(baseSlug)) return baseSlug;

        int suffix = 2;
        while (existingSlugs.Contains($"{baseSlug}-{suffix}")) suffix++;
        return $"{baseSlug}-{suffix}";
    }
}
