using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side write implementation for Series (Feature 9, WU41). Inherits the read path via
/// primary-constructor chaining, mirroring <see cref="ServerGroupWriteService"/>. A series holds only
/// the owner's own stories — every membership mutation checks
/// <c>Story.AuthorId == Series.AuthorId == ActiveUser.UserId</c> (WU41 settled decision; see
/// <c>audit/Stories.md</c> Feature 9). Sanitizes <see cref="Series.Description"/> once on save.
/// </summary>
public class ServerSeriesWriteService(
    ApplicationDbContext writeDb,
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser,
    IHtmlSanitizationService sanitizer)
    : ServerSeriesReadService(readDbFactory, activeUser), ISeriesWriteService
{
    // ── Series CRUD ───────────────────────────────────────────────────────────────

    public async Task<int> CreateSeriesAsync(CreateSeriesDto dto)
    {
        int creatorId = ActiveUser.RequireUserId();

        List<string> errors = dto.CanSave();

        string trimmedName = dto.Name.Trim();
        bool nameExists = await writeDb.Series
            .AnyAsync(s => s.AuthorId == creatorId && s.Name.ToLower() == trimmedName.ToLower());
        if (nameExists)
            errors.Add($"You already have a series named \"{trimmedName}\".");

        if (errors.Count > 0) throw new SeriesValidationException(errors);

        Series series = new()
        {
            AuthorId    = creatorId,
            Name        = trimmedName,
            Description = dto.Description is not null ? sanitizer.Sanitize(dto.Description) : null,
            DateCreated = DateTime.UtcNow
        };

        writeDb.Series.Add(series);
        await writeDb.SaveChangesAsync();

        return series.SeriesId;
    }

    public async Task UpdateSeriesAsync(UpdateSeriesDto dto)
    {
        int userId = ActiveUser.RequireUserId();

        Series? series = await writeDb.Series.FirstOrDefaultAsync(s => s.SeriesId == dto.SeriesId);
        if (series is null) throw new KeyNotFoundException($"Series {dto.SeriesId} not found.");
        RequireOwner(series, userId);

        List<string> errors = dto.CanSave();

        string trimmedName = dto.Name.Trim();
        bool nameExists = await writeDb.Series
            .AnyAsync(s => s.SeriesId != dto.SeriesId && s.AuthorId == userId
                        && s.Name.ToLower() == trimmedName.ToLower());
        if (nameExists)
            errors.Add($"You already have a series named \"{trimmedName}\".");

        if (errors.Count > 0) throw new SeriesValidationException(errors);

        series.Name        = trimmedName;
        series.Description = dto.Description is not null ? sanitizer.Sanitize(dto.Description) : null;

        await writeDb.SaveChangesAsync();
    }

    public async Task DeleteSeriesAsync(int seriesId)
    {
        int userId = ActiveUser.RequireUserId();

        Series? series = await writeDb.Series.FirstOrDefaultAsync(s => s.SeriesId == seriesId);
        if (series is null) throw new KeyNotFoundException($"Series {seriesId} not found.");
        RequireOwner(series, userId);

        // SeriesEntry rows cascade from Series (StoryConfigurations.SeriesConfiguration); member
        // stories themselves are untouched — only the grouping is removed.
        writeDb.Series.Remove(series);
        await writeDb.SaveChangesAsync();
    }

    // ── Membership ────────────────────────────────────────────────────────────────

    public async Task AddStoryAsync(int seriesId, int storyId)
    {
        int userId = ActiveUser.RequireUserId();

        Series? series = await writeDb.Series.FirstOrDefaultAsync(s => s.SeriesId == seriesId);
        if (series is null) throw new KeyNotFoundException($"Series {seriesId} not found.");
        RequireOwner(series, userId);

        Story? story = await writeDb.Stories.FirstOrDefaultAsync(s => s.StoryId == storyId);
        if (story is null) throw new KeyNotFoundException($"Story {storyId} not found.");

        // A series holds only the owner's own stories (WU41 settled decision).
        if (story.AuthorId != userId)
            throw new UnauthorizedAccessException("You can only add your own stories to a series.");

        bool alreadyMember = await writeDb.SeriesEntries
            .AnyAsync(se => se.SeriesId == seriesId && se.StoryId == storyId);
        if (alreadyMember) return; // idempotent

        // Next OrderIndex = max existing + 1 (append to the end).
        int nextIndex = await writeDb.SeriesEntries
            .Where(se => se.SeriesId == seriesId)
            .Select(se => (int?)se.OrderIndex)
            .MaxAsync() ?? -1;
        nextIndex++;

        writeDb.SeriesEntries.Add(new SeriesEntry
        {
            SeriesId   = seriesId,
            StoryId    = storyId,
            OrderIndex = nextIndex
        });
        await writeDb.SaveChangesAsync();
    }

    public async Task RemoveStoryAsync(int seriesId, int storyId)
    {
        int userId = ActiveUser.RequireUserId();

        Series? series = await writeDb.Series.FirstOrDefaultAsync(s => s.SeriesId == seriesId);
        if (series is null) throw new KeyNotFoundException($"Series {seriesId} not found.");
        RequireOwner(series, userId);

        SeriesEntry? entry = await writeDb.SeriesEntries
            .FirstOrDefaultAsync(se => se.SeriesId == seriesId && se.StoryId == storyId);
        if (entry is null) return; // idempotent — no-op if not a member

        writeDb.SeriesEntries.Remove(entry);
        await writeDb.SaveChangesAsync();
    }

    public async Task ReorderAsync(int seriesId, IReadOnlyList<int> orderedStoryIds)
    {
        int userId = ActiveUser.RequireUserId();

        Series? series = await writeDb.Series.FirstOrDefaultAsync(s => s.SeriesId == seriesId);
        if (series is null) throw new KeyNotFoundException($"Series {seriesId} not found.");
        RequireOwner(series, userId);

        List<SeriesEntry> entries = await writeDb.SeriesEntries
            .Where(se => se.SeriesId == seriesId)
            .ToListAsync();

        // Reject unless the given id set is exactly this series' current membership — no partial
        // reorders, no smuggling in an id that was never added via AddStoryAsync.
        HashSet<int> currentIds = entries.Select(e => e.StoryId).ToHashSet();
        if (orderedStoryIds.Count != currentIds.Count || !currentIds.SetEquals(orderedStoryIds))
            throw new SeriesValidationException(
                ["The provided story order doesn't match this series' current membership."]);

        Dictionary<int, SeriesEntry> byStoryId = entries.ToDictionary(e => e.StoryId);
        for (int i = 0; i < orderedStoryIds.Count; i++)
            byStoryId[orderedStoryIds[i]].OrderIndex = i;

        await writeDb.SaveChangesAsync();
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private static void RequireOwner(Series series, int userId)
    {
        if (series.AuthorId != userId)
            throw new UnauthorizedAccessException("You must be the owner of this series.");
    }
}
