using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side write implementation for Story Arcs (Feature 8, WU45). Author-gated CRUD with the
/// WU45 range rules enforced here (deliberately business logic, not DB constraints): Start ≥ 1,
/// Start ≤ End, no overlap with any other arc of the same story, unique title per story. An arc
/// range MAY extend past the story's current last chapter (authors plan future volumes); the
/// reader-facing segmenter simply skips arcs that cover no visible chapter. Mirrors
/// <see cref="ServerSeriesWriteService"/>'s gate/validation shape.
/// </summary>
public class ServerStoryArcWriteService(
    ApplicationDbContext writeDb,
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser)
    : ServerStoryArcReadService(readDbFactory), IStoryArcWriteService
{
    private readonly ApplicationDbContext _writeDb = writeDb;
    // No CS9107 concern: the read base takes no IActiveUserContext, so this class is the sole owner.
    private readonly IActiveUserContext _activeUser = activeUser;

    public async Task<int> CreateArcAsync(CreateStoryArcDto dto)
    {
        int userId = _activeUser.RequireUserId();

        // Write-context lookups see ground truth (no named query filters on ApplicationDbContext).
        Story? story = await _writeDb.Stories.FirstOrDefaultAsync(s => s.StoryId == dto.StoryId);
        if (story is null) throw new KeyNotFoundException($"Story {dto.StoryId} not found.");
        if (story.AuthorId != userId)
            throw new UnauthorizedAccessException("You must be the author of this story.");

        string title = dto.Title?.Trim() ?? string.Empty;
        List<string> errors = await ValidateAsync(
            dto.StoryId, excludeArcId: null, title, dto.StartChapterNumber, dto.EndChapterNumber);
        if (errors.Count > 0) throw new StoryArcValidationException(errors);

        StoryArc arc = new()
        {
            StoryId            = dto.StoryId,
            Title              = title,
            StartChapterNumber = dto.StartChapterNumber,
            EndChapterNumber   = dto.EndChapterNumber
        };
        _writeDb.StoryArcs.Add(arc);
        await _writeDb.SaveChangesAsync();
        return arc.StoryArcId;
    }

    public async Task UpdateArcAsync(UpdateStoryArcDto dto)
    {
        int userId = _activeUser.RequireUserId();

        StoryArc? arc = await _writeDb.StoryArcs
            .Include(a => a.Story)
            .FirstOrDefaultAsync(a => a.StoryArcId == dto.StoryArcId);
        if (arc is null) throw new KeyNotFoundException($"Story arc {dto.StoryArcId} not found.");
        if (arc.Story.AuthorId != userId)
            throw new UnauthorizedAccessException("You must be the author of this story.");

        string title = dto.Title?.Trim() ?? string.Empty;
        List<string> errors = await ValidateAsync(
            arc.StoryId, excludeArcId: arc.StoryArcId, title, dto.StartChapterNumber, dto.EndChapterNumber);
        if (errors.Count > 0) throw new StoryArcValidationException(errors);

        arc.Title              = title;
        arc.StartChapterNumber = dto.StartChapterNumber;
        arc.EndChapterNumber   = dto.EndChapterNumber;
        await _writeDb.SaveChangesAsync();
    }

    public async Task DeleteArcAsync(int storyArcId)
    {
        int userId = _activeUser.RequireUserId();

        StoryArc? arc = await _writeDb.StoryArcs
            .Include(a => a.Story)
            .FirstOrDefaultAsync(a => a.StoryArcId == storyArcId);
        if (arc is null) throw new KeyNotFoundException($"Story arc {storyArcId} not found.");
        if (arc.Story.AuthorId != userId)
            throw new UnauthorizedAccessException("You must be the author of this story.");

        // The covered chapters simply become arc-less (gap) chapters — nothing else to touch.
        _writeDb.StoryArcs.Remove(arc);
        await _writeDb.SaveChangesAsync();
    }

    // ── Validation core (WU45 range rules) ────────────────────────────────────────

    private async Task<List<string>> ValidateAsync(
        int storyId, int? excludeArcId, string title, int start, int end)
    {
        List<string> errors = [];

        if (title.Length == 0) errors.Add("Arc title is required.");
        if (title.Length > 255) errors.Add("Arc title must be 255 characters or fewer.");
        if (start < 1) errors.Add("Start chapter must be 1 or greater.");
        if (end < start) errors.Add("End chapter must not be before the start chapter.");

        if (errors.Count > 0) return errors; // range checks below assume a sane range

        // Friendly pre-checks for the two rules the service owns (title uniqueness is also backed
        // by the unique (story_id, title) index as a race backstop).
        List<StoryArc> siblings = await _writeDb.StoryArcs
            .Where(a => a.StoryId == storyId
                     && (excludeArcId == null || a.StoryArcId != excludeArcId))
            .ToListAsync();

        if (siblings.Any(a => a.Title.ToLowerInvariant() == title.ToLowerInvariant()))
            errors.Add($"This story already has an arc named \"{title}\".");

        StoryArc? overlap = siblings.FirstOrDefault(a =>
            start <= a.EndChapterNumber && end >= a.StartChapterNumber);
        if (overlap is not null)
            errors.Add($"Chapters {start}–{end} overlap the arc \"{overlap.Title}\" " +
                       $"(chapters {overlap.StartChapterNumber}–{overlap.EndChapterNumber}). " +
                       "Arcs cannot overlap.");

        return errors;
    }

}
