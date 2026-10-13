using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side <see cref="IContentRevealService"/> (WU-AccessGate). Reads resolve display titles
/// through elevated queries — Personal plane: the member consented to these items, so their own
/// management list is never re-filtered by their current ceiling (that would recreate the
/// ghost-row trap this feature exists to prevent). The elevation is rating/audience only:
/// confidentiality still applies (story status and takedown through the read context's filters; a
/// blog post's publication and its author's <c>ProfileVisibility</c> through
/// <see cref="BlogPostVisibilityGuard.IsVisible"/>), and a hidden item lists under the same
/// "(deleted …)" placeholder as an absent one. Removal is self-scoped by construction (the key
/// includes the caller's own id).
///
/// <para><see cref="ResolveVisibleTitlesAsync"/> is the one copy of that rule. The list titles its
/// rows with it, and <see cref="ContentRevealRecorder"/> mints a row only for a target it returns
/// (WU-ThrottleCoverage review fixes, 2026-10-13). So the list never shows a "(deleted …)" row for an
/// id that was hidden when it was revealed, and it cannot tell a hidden id from a missing one. Before
/// that, the recorder checked ground truth: a hidden id left a "(deleted …)" row and a missing id
/// left none, an existence oracle over every hidden story and post.</para>
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

        Dictionary<(RevealedEntityType, int), string> titles = await ResolveVisibleTitlesAsync(
            readDb, activeUser, [.. reveals.Select(r => (r.EntityType, r.EntityId))]);

        return reveals
            .Select(r => new RevealDisplayDto(
                r.EntityType,
                r.EntityId,
                titles.TryGetValue((r.EntityType, r.EntityId), out string? title)
                    ? title
                    : r.EntityType switch
                    {
                        RevealedEntityType.Story => "(deleted story)",
                        RevealedEntityType.Group => "(deleted group)",
                        RevealedEntityType.BlogPost => "(deleted post)",
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

    /// <summary>
    /// The titles of the reveal targets <paramref name="viewer"/> may see once the rating/audience gate
    /// is lifted (Personal plane, see class doc). A target missing from the result is absent or hidden,
    /// and the two are never told apart. The type → set mapping: a <see cref="RevealedEntityType.BlogPost"/>
    /// is a profile post; a group post reveals its group.
    /// </summary>
    public static async Task<Dictionary<(RevealedEntityType Type, int Id), string>> ResolveVisibleTitlesAsync(
        ReadOnlyApplicationDbContext readDb,
        IActiveUserContext viewer,
        IReadOnlyCollection<(RevealedEntityType Type, int Id)> targets)
    {
        HashSet<int> storyIds = [.. targets.Where(t => t.Type == RevealedEntityType.Story).Select(t => t.Id)];
        HashSet<int> groupIds = [.. targets.Where(t => t.Type == RevealedEntityType.Group).Select(t => t.Id)];
        HashSet<int> postIds = [.. targets.Where(t => t.Type == RevealedEntityType.BlogPost).Select(t => t.Id)];

        Dictionary<(RevealedEntityType, int), string> titles = [];

        if (storyIds.Count > 0)
        {
            // StoryStatus (author-aware) and IsTakenDown still apply: confidentiality, which no
            // consent bypasses (ReadOnlyApplicationDbContext).
            var stories = await readDb.Stories
                .IgnoreQueryFilters(["ContentRating"]) // elevated read: Personal plane (own reveals)
                .Where(s => storyIds.Contains(s.StoryId))
                .Select(s => new { s.StoryId, Title = s.StoryListing != null ? s.StoryListing.StoryTitle : "" })
                .ToListAsync();
            foreach (var s in stories) titles[(RevealedEntityType.Story, s.StoryId)] = s.Title;
        }

        if (groupIds.Count > 0)
        {
            var groups = await readDb.Groups
                .IgnoreQueryFilters(["GroupAudience"]) // elevated read: Personal plane (own reveals)
                .Where(g => groupIds.Contains(g.GroupId))
                .Select(g => new { g.GroupId, g.GroupName })
                .ToListAsync();
            foreach (var g in groups) titles[(RevealedEntityType.Group, g.GroupId)] = g.GroupName;
        }

        if (postIds.Count > 0)
        {
            // A blog post carries no rating filter to lift (TPT, WU31.5), so the post's own visibility
            // rule decides. The rating axis is satisfied by the reveal itself (isRevealed: true), but
            // confidentiality is not: an unpublished draft, or a profile post whose author's
            // ProfileVisibility hides it (Class A, WU-AccessGateSweep2), stays out. A row can also
            // outlive its target's visibility (the post unpublished or its author gone Private after
            // the reveal); it then lists as "(deleted post)", the placeholder a story reveal gets once
            // the StoryStatus/IsTakenDown filters drop it.
            var posts = await readDb.ProfileBlogPosts
                .Where(p => postIds.Contains(p.BlogPostId))
                .Select(p => new
                {
                    p.BlogPostId, p.Title, p.AuthorId, p.IsPublished, p.Rating,
                    AuthorProfileVisibility = p.Author != null
                        ? (ProfileVisibility?)p.Author.PrivacySettings.ProfileVisibility
                        : null,
                })
                .ToListAsync();

            foreach (var p in posts.Where(p => BlogPostVisibilityGuard.IsVisible(
                         new BlogPostVisibilityFacts(p.BlogPostId, p.AuthorId, p.IsPublished, p.Rating,
                             IsGroupPost: false, GroupId: null, GroupAudience: null, p.AuthorProfileVisibility),
                         viewer, isRevealed: true)))
            {
                titles[(RevealedEntityType.BlogPost, p.BlogPostId)] = p.Title;
            }
        }

        return titles;
    }
}
