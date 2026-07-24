using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side <see cref="IContentRevealService"/> (WU-AccessGate). Reads resolve display titles
/// through elevated queries — Personal plane: the member consented to these items, so their own
/// management list is never re-filtered by their current ceiling (that would recreate the
/// ghost-row trap this feature exists to prevent). Removal is self-scoped by construction (the
/// key includes the caller's own id).
/// </summary>
public class ServerContentRevealService(
    ApplicationDbContext writeDb,
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser) : IContentRevealService
{
    public async Task<IReadOnlyList<RevealDisplayDto>> GetMyRevealsAsync()
    {
        if (activeUser.UserId is not int userId) return [];

        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();

        List<UserContentReveal> reveals = await readDb.UserContentReveals
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.DateRevealed)
            .ToListAsync();
        if (reveals.Count == 0) return [];

        // Batch title lookups per entity type — elevated reads (see class doc).
        HashSet<int> storyIds = [.. reveals.Where(r => r.EntityType == RevealedEntityType.Story).Select(r => r.EntityId)];
        HashSet<int> groupIds = [.. reveals.Where(r => r.EntityType == RevealedEntityType.Group).Select(r => r.EntityId)];
        HashSet<int> postIds = [.. reveals.Where(r => r.EntityType == RevealedEntityType.BlogPost).Select(r => r.EntityId)];

        Dictionary<int, string> storyTitles = storyIds.Count == 0 ? [] :
            await readDb.Stories
                .IgnoreQueryFilters(["ContentRating"]) // elevated read: Personal plane (own reveals)
                .Where(s => storyIds.Contains(s.StoryId))
                .Select(s => new { s.StoryId, Title = s.StoryListing != null ? s.StoryListing.StoryTitle : "" })
                .ToDictionaryAsync(x => x.StoryId, x => x.Title);

        Dictionary<int, string> groupNames = groupIds.Count == 0 ? [] :
            await readDb.Groups
                .IgnoreQueryFilters(["GroupAudience"]) // elevated read: Personal plane (own reveals)
                .Where(g => groupIds.Contains(g.GroupId))
                .Select(g => new { g.GroupId, g.GroupName })
                .ToDictionaryAsync(x => x.GroupId, x => x.GroupName);

        Dictionary<int, string> postTitles = postIds.Count == 0 ? [] :
            await readDb.ProfileBlogPosts
                .Where(p => postIds.Contains(p.BlogPostId))
                .Select(p => new { p.BlogPostId, p.Title })
                .ToDictionaryAsync(x => x.BlogPostId, x => x.Title);

        return reveals
            .Select(r => new RevealDisplayDto(
                r.EntityType,
                r.EntityId,
                r.EntityType switch
                {
                    RevealedEntityType.Story => storyTitles.GetValueOrDefault(r.EntityId, "(deleted story)"),
                    RevealedEntityType.Group => groupNames.GetValueOrDefault(r.EntityId, "(deleted group)"),
                    RevealedEntityType.BlogPost => postTitles.GetValueOrDefault(r.EntityId, "(deleted post)"),
                    _ => "(unknown)",
                },
                r.DateRevealed))
            .ToList();
    }

    public async Task RemoveAsync(RevealedEntityType entityType, int entityId)
    {
        if (activeUser.UserId is not int userId)
            throw new InvalidOperationException("Managing reveals requires an authenticated user.");

        UserContentReveal? row = await writeDb.UserContentReveals.FindAsync(userId, entityType, entityId);
        if (row is null) return; // already gone — removal is idempotent

        writeDb.UserContentReveals.Remove(row);
        await writeDb.SaveChangesAsync();
    }
}
