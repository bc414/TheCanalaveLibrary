using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Buffered reading-progress writer (Feature 44 L2). Pings land in the in-process
/// <see cref="ReadingProgressBuffer"/> (O(1) coalescing merge, no DB hit);
/// <see cref="ReadingProgressFlushWorker"/> batch-flushes on its cadence. Contract per the
/// interface: eventually-durable, loss window = one flush interval.
/// </summary>
public class ServerReadingProgressWriteService(
    ReadingProgressBuffer buffer,
    IActiveUserContext activeUser) : IReadingProgressWriteService
{
    public Task RecordProgressAsync(int chapterId, float progress)
    {
        // Anonymous viewers no-op (interface contract).
        if (activeUser.UserId is int userId)
            buffer.Record(userId, chapterId, progress);
        return Task.CompletedTask;
    }
}
