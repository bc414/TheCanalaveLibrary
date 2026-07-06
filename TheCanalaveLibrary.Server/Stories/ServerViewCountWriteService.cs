using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Buffered view recorder (Feature 45 L2). Views land in the in-process
/// <see cref="ViewCountBuffer"/> (O(1) increment, no DB hit); <see cref="ViewCountFlushWorker"/>
/// batch-flushes into <c>daily_story_stats</c>. Anonymous views count — no user context needed.
/// </summary>
public class ServerViewCountWriteService(ViewCountBuffer buffer) : IViewCountWriteService
{
    public Task RecordViewAsync(int storyId)
    {
        buffer.Record(storyId);
        return Task.CompletedTask;
    }
}
