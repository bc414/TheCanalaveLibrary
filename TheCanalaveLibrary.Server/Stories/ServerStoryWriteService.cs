using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

public class ServerStoryWriteService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser,
    IImageStorageService imageStorage)
    : ServerStoryReadService(readDbFactory, activeUser), IStoryWriteService
{
    public async Task<int> CreateStoryAsync(CreateStoryDTO newStoryDTO)
    {
        // AuthorId is always server-stamped; CreateStoryDTO intentionally has no AuthorId property.
        if (ActiveUser.UserId is not int authorId)
            throw new InvalidOperationException("Creating a story requires an authenticated user.");

        List<string> validationErrors = newStoryDTO.CanSave();
        if (validationErrors.Any())
            throw new StoryValidationException(validationErrors);

        await ValidateStructuredTagGatesAsync(newStoryDTO, writeDb);

        Story newStoryDB = newStoryDTO.ToStory();
        newStoryDB.AuthorId = authorId;
        // Slug is server-only and never client-editable (not on CreateStoryDTO at all) — settled WU12.
        newStoryDB.StoryDetail.Slug = await GenerateUniqueSlugAsync(newStoryDTO.Title);

        // Story.StoryListing/StoryDetail are reachable navigations on a connected object graph — Add()
        // tracks the whole graph as Added, so no separate Attach() is needed. The original code's
        // Attach() calls were a real bug, not just redundant: Attach marks an entity Unchanged, which
        // would make SaveChangesAsync skip inserting the listing/detail rows entirely (fixed WU12,
        // alongside the ToStory() NRE this depended on — see StoryMappers.cs).
        writeDb.Stories.Add(newStoryDB);
        await writeDb.SaveChangesAsync();

        // Increment StoriesWritten counter for the author (cross-cutting.md §"UserStats Updates").
        await writeDb.UserStats.Where(us => us.UserId == authorId)
            .ExecuteUpdateAsync(s => s.SetProperty(us => us.StoriesWritten, us => us.StoriesWritten + 1));

        return newStoryDB.StoryId;
    }

    public async Task UpdateStoryAsync(StoryUpdateDTO dto)
    {
        List<string> validationErrors = dto.CanSave();
        if (validationErrors.Any())
            throw new StoryValidationException(validationErrors);

        Story? storyToUpdate = await writeDb.Stories
            .Include(s => s.StoryListing)
            .Include(s => s.StoryDetail)
            .Include(s => s.StoryTags)
            .Include(s => s.StoryCharacters)
            .Include(s => s.StoryCharacterPairings).ThenInclude(scp => scp.Members)
            .Include(s => s.SettingDetails)
            .FirstOrDefaultAsync(s => s.StoryId == dto.StoryId);

        if (storyToUpdate is null)
            throw new KeyNotFoundException($"Story with ID {dto.StoryId} not found.");

        // Author-only gate (cross-cutting.md "Security vs affordance"). Moderation actions (WU34) use
        // a separate admin service path — they do NOT OR into this ownership check.
        if (storyToUpdate.AuthorId != ActiveUser.UserId)
            throw new UnauthorizedAccessException("You can only edit your own stories.");

        await ValidateStructuredTagGatesAsync(dto, writeDb);

        // Capture the old cover path before overwriting (orphan-bug fix — DeleteAsync had zero callers).
        string? oldCoverPath = storyToUpdate.StoryListing?.CoverArtRelativeUrl;

        storyToUpdate.UpdateStoryEditableProperties(dto);

        await writeDb.SaveChangesAsync();

        // Best-effort cleanup of the old cover blob when it changes. A failed delete must not
        // propagate — the story update already succeeded. The null/same-path guard prevents
        // accidental deletion when the cover hasn't actually changed.
        if (oldCoverPath is not null && oldCoverPath != dto.CoverArtRelativeUrl)
        {
            try { await imageStorage.DeleteAsync(oldCoverPath); }
            catch { /* best-effort; log in a future structured-logging pass */ }
        }
    }

    /// <summary>
    /// Server-side gate checks that require DB reads.
    /// CanSave() handles client-verifiable rules; this handles rules that trust only server Tag state.
    /// </summary>
    private static async Task ValidateStructuredTagGatesAsync(
        IEditableStoryProperties dto, ApplicationDbContext writeDb)
    {
        List<string> errors = [];

        // OC gate: character tags that carry OC data must have AllowOCDetails == true.
        List<int> ocCharTagIds = dto.StoryCharacters
            .Where(sc => sc.IsOc || sc.OcName != null || sc.OcBio != null)
            .Select(sc => sc.CharacterTagId).Distinct().ToList();
        if (ocCharTagIds.Count > 0)
        {
            List<int> disallowed = await writeDb.Tags
                .Where(t => ocCharTagIds.Contains(t.TagId) && !t.AllowOCDetails)
                .Select(t => t.TagId).ToListAsync();
            if (disallowed.Count > 0)
                errors.Add($"Character tag(s) {string.Join(", ", disallowed)} do not allow OC details.");
        }

        // SettingDetail gate: tags with custom detail data must have AllowSettingDetails == true.
        List<int> detailSettingTagIds = dto.SettingDetails
            .Where(sd => sd.Name != null || sd.Description != null)
            .Select(sd => sd.BaseTagId).Distinct().ToList();
        if (detailSettingTagIds.Count > 0)
        {
            List<int> disallowed = await writeDb.Tags
                .Where(t => detailSettingTagIds.Contains(t.TagId) && !t.AllowSettingDetails)
                .Select(t => t.TagId).ToListAsync();
            if (disallowed.Count > 0)
                errors.Add($"Setting tag(s) {string.Join(", ", disallowed)} do not allow custom details.");
        }

        // Pairing member count: each pairing needs ≥2 members.
        if (dto.StoryCharacterPairings.Any(p => p.MemberCharacterTagIds.Count < 2))
            errors.Add("Each character pairing must include at least 2 members.");

        // Pairing members must all be in the story's character list.
        HashSet<int> storyCharTagIds = dto.StoryCharacters.Select(sc => sc.CharacterTagId).ToHashSet();
        bool orphanedMembers = dto.StoryCharacterPairings
            .SelectMany(p => p.MemberCharacterTagIds)
            .Any(tagId => !storyCharTagIds.Contains(tagId));
        if (orphanedMembers)
            errors.Add("Pairing members must all be in the story's character list.");

        if (errors.Count > 0) throw new StoryValidationException(errors);
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
