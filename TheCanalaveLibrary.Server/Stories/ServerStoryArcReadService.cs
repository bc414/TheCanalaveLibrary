using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side read implementation for Story Arcs (Feature 8, WU45). Factory-per-method read
/// context per layer2-services.md §"Read-Context Concurrency".
/// <para>
/// Takes <see cref="IActiveUserContext"/> for the kind-(g) parent gate (WU-ParentVisibility). Until
/// then this was the only service in the codebase injecting no user context at all, which meant it
/// was structurally incapable of gating anything: arc titles and chapter ranges — a story's whole
/// narrative skeleton — were readable anonymously for M-unrevealed, Draft/PendingApproval/Rejected
/// and taken-down stories.
/// </para>
/// </summary>
public class ServerStoryArcReadService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser) : IStoryArcReadService
{
    // Protected for the derived write service's read-side lookups (CS9107 pattern).
    protected IDbContextFactory<ReadOnlyApplicationDbContext> ReadDbFactory { get; } = readDbFactory;
    protected IActiveUserContext ActiveUser { get; } = activeUser;

    public async Task<IReadOnlyList<StoryArcDto>> GetArcsForStoryAsync(int storyId)
    {
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        // Kind (g): arcs are exactly as visible as their story.
        if (!await StoryVisibilityGuard.IsStoryVisibleAsync(readDb, ActiveUser, storyId))
            return [];

        return await readDb.StoryArcs
            .Where(a => a.StoryId == storyId)
            .OrderBy(a => a.StartChapterNumber)
            .Select(a => new StoryArcDto(
                a.StoryArcId, a.Title, a.StartChapterNumber, a.EndChapterNumber))
            .ToArrayAsync();
    }
}
