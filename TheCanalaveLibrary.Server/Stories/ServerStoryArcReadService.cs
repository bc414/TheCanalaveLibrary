using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side read implementation for Story Arcs (Feature 8, WU45). Factory-per-method read
/// context per layer2-services.md §"Read-Context Concurrency".
/// </summary>
public class ServerStoryArcReadService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory) : IStoryArcReadService
{
    // Protected for the derived write service's read-side lookups (CS9107 pattern).
    protected IDbContextFactory<ReadOnlyApplicationDbContext> ReadDbFactory { get; } = readDbFactory;

    public async Task<IReadOnlyList<StoryArcDto>> GetArcsForStoryAsync(int storyId)
    {
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();
        return await readDb.StoryArcs
            .Where(a => a.StoryId == storyId)
            .OrderBy(a => a.StartChapterNumber)
            .Select(a => new StoryArcDto(
                a.StoryArcId, a.Title, a.StartChapterNumber, a.EndChapterNumber))
            .ToArrayAsync();
    }
}
