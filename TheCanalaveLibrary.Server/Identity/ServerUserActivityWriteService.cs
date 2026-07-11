using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Buffered activity recorder (WU-SiteDailyStat, Feature 62 L2). Pings land in the in-process
/// <see cref="UserActivityBuffer"/> (O(1) merge, no DB hit); <see cref="UserActivityFlushWorker"/>
/// batch-flushes into <c>User.LastActiveUtc</c>.
/// </summary>
public class ServerUserActivityWriteService(UserActivityBuffer buffer) : IUserActivityWriteService
{
    public Task RecordActivityAsync(int userId)
    {
        buffer.Record(userId);
        return Task.CompletedTask;
    }
}
