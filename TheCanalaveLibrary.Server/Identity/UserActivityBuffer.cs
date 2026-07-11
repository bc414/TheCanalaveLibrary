using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Singleton in-process coalescing buffer for <see cref="User.LastActiveUtc"/> pings
/// (WU-SiteDailyStat, Feature 62 L2 — the signal-buffering pattern, layer2-services.md; sibling of
/// <see cref="ReadingProgressBuffer"/>/<see cref="ViewCountBuffer"/>). Coalescing is a per-user
/// <b>latest-timestamp</b> merge: many pings for the same user in a flush window collapse to one
/// entry. <see cref="UserActivityFlushWorker"/> drains into <c>User.LastActiveUtc</c>. Contract:
/// eventually-durable, loss window = one flush interval — an activity stamp is approximate by
/// design, never a security/authorization signal.
///
/// At N≥2 web nodes this swaps for a shared RESP store behind the same
/// <see cref="IUserActivityWriteService"/> — a body swap, no interface change (horizontal-scaling.md).
/// </summary>
public sealed class UserActivityBuffer
{
    private readonly ConcurrentDictionary<int, DateTime> _lastActiveByUserId = new();

    // Not IDisposable — same rationale as the sibling buffers' gauges (shared static Meter; one
    // buffer per production process; test-host duplicates are benign).
    private readonly ObservableGauge<int> _depthGauge;

    public UserActivityBuffer()
    {
        _depthGauge = CanalaveTelemetry.UserActivity.Meter.CreateObservableGauge(
            "canalave.useractivity.buffer.depth",
            () => _lastActiveByUserId.Count,
            unit: "{user}",
            description: "Distinct users with an activity ping currently pending flush.");
    }

    /// <summary>Distinct users with a pending activity stamp.</summary>
    public int Count => _lastActiveByUserId.Count;

    /// <summary>Records "now" as the latest activity timestamp for <paramref name="userId"/>.</summary>
    public void Record(int userId)
    {
        DateTime nowUtc = DateTime.UtcNow;
        _lastActiveByUserId.AddOrUpdate(userId, nowUtc, (_, existing) => nowUtc > existing ? nowUtc : existing);
    }

    /// <summary>
    /// Atomically removes and returns all pending (userId, lastActiveUtc) pairs. A ping racing the
    /// drain either merges before removal (this batch) or re-adds afterward (next batch) — never lost.
    /// </summary>
    public List<(int UserId, DateTime LastActiveUtc)> Drain()
    {
        var drained = new List<(int, DateTime)>(_lastActiveByUserId.Count);
        foreach (int userId in _lastActiveByUserId.Keys)
        {
            if (_lastActiveByUserId.TryRemove(userId, out DateTime lastActiveUtc))
                drained.Add((userId, lastActiveUtc));
        }
        return drained;
    }

    /// <summary>Merges a previously drained batch back after a failed flush (latest-wins merge).</summary>
    public void Restore(IEnumerable<(int UserId, DateTime LastActiveUtc)> batch)
    {
        foreach ((int userId, DateTime lastActiveUtc) in batch)
        {
            _lastActiveByUserId.AddOrUpdate(
                userId, lastActiveUtc,
                (_, existing) => lastActiveUtc > existing ? lastActiveUtc : existing);
        }
    }

    /// <summary>Discards all pending entries. Test isolation only — never called in production.</summary>
    public void Clear() => _lastActiveByUserId.Clear();
}
