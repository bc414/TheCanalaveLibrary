using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Singleton in-process coalescing buffer for reading-progress pings (Feature 44 L2 — the
/// signal-buffering pattern, layer2-services.md). Every active reader's throttled scroll pings
/// land here as an O(1) dictionary merge instead of a per-ping database write; the
/// <see cref="ReadingProgressFlushWorker"/> drains the buffer on a fixed cadence into one batched
/// upsert.
///
/// Coalescing keeps <b>max progress + latest timestamp</b> per (user, chapter) — N pings in a
/// flush window become one entry. Contract: eventually-durable, may lose the last flush-interval's
/// pings on a crash (loss-tolerant signal by design; deliberate actions like HasStarted take the
/// durable direct-write path in IUserStoryInteractionWriteService, never this buffer).
///
/// At N≥2 web nodes this in-process store swaps for a shared RESP store (Valkey hash) behind the
/// same <see cref="IReadingProgressWriteService"/> — a body swap, no interface change.
/// </summary>
public sealed class ReadingProgressBuffer
{
    /// <summary>Coalesced pending write: high-water progress + most recent ping time.</summary>
    public readonly record struct Entry(float MaxProgress, DateTime LastTimestampUtc);

    private readonly ConcurrentDictionary<(int UserId, int ChapterId), Entry> _entries = new();

    // Deliberately not IDisposable: instruments on the shared static Meter can't be individually
    // unregistered. Production has exactly one buffer per process; extra registrations from
    // integration-test hosts are benign duplicate observations.
    private readonly ObservableGauge<int> _depthGauge;

    public ReadingProgressBuffer()
    {
        _depthGauge = CanalaveTelemetry.ReadingProgress.Meter.CreateObservableGauge(
            "canalave.readingprogress.buffer.depth",
            () => _entries.Count,
            unit: "{entry}",
            description: "Coalesced (user, chapter) entries currently pending flush.");
    }

    /// <summary>Number of coalesced entries currently pending flush.</summary>
    public int Count => _entries.Count;

    /// <summary>
    /// Merges one ping. Keeps the maximum progress seen and stamps the current time — repeated
    /// pings for the same (user, chapter) collapse into a single pending entry.
    /// </summary>
    public void Record(int userId, int chapterId, float progress)
    {
        DateTime nowUtc = DateTime.UtcNow;
        _entries.AddOrUpdate(
            (userId, chapterId),
            _ => new Entry(progress, nowUtc),
            (_, existing) => new Entry(Math.Max(existing.MaxProgress, progress), nowUtc));
    }

    /// <summary>
    /// Atomically removes and returns all pending entries. A ping racing the drain either merges
    /// before its key is removed (included in this batch) or re-adds afterward (next batch) —
    /// never lost. Called by the flusher; tests may call it directly.
    /// </summary>
    public List<(int UserId, int ChapterId, float MaxProgress, DateTime LastTimestampUtc)> Drain()
    {
        var drained = new List<(int, int, float, DateTime)>(_entries.Count);
        foreach ((int UserId, int ChapterId) key in _entries.Keys)
        {
            if (_entries.TryRemove(key, out Entry entry))
                drained.Add((key.UserId, key.ChapterId, entry.MaxProgress, entry.LastTimestampUtc));
        }
        return drained;
    }

    /// <summary>
    /// Merges a previously drained batch back after a failed flush so it retries next cycle
    /// (max/latest semantics — concurrent pings recorded meanwhile are never overwritten down).
    /// </summary>
    public void Restore(IEnumerable<(int UserId, int ChapterId, float MaxProgress, DateTime LastTimestampUtc)> batch)
    {
        foreach ((int userId, int chapterId, float maxProgress, DateTime lastTs) in batch)
        {
            _entries.AddOrUpdate(
                (userId, chapterId),
                _ => new Entry(maxProgress, lastTs),
                (_, existing) => new Entry(
                    Math.Max(existing.MaxProgress, maxProgress),
                    existing.LastTimestampUtc >= lastTs ? existing.LastTimestampUtc : lastTs));
        }
    }

    /// <summary>
    /// Discards any pending ping for one (user, chapter). Called by the durable manual-mark path
    /// (WU45, <c>ServerChapterReadMarkWriteService</c>) so an in-flight scroll ping can't flush
    /// after a manual mark and resurrect the overridden read state (the flusher's high-water merge
    /// would otherwise re-raise a manually-reset ReadProgress).
    /// </summary>
    public void Discard(int userId, int chapterId) => _entries.TryRemove((userId, chapterId), out _);

    /// <summary>Bulk <see cref="Discard(int,int)"/> for mark-all (WU45).</summary>
    public void Discard(int userId, IEnumerable<int> chapterIds)
    {
        foreach (int chapterId in chapterIds)
            _entries.TryRemove((userId, chapterId), out _);
    }

    /// <summary>Discards all pending entries. Test isolation only — never called in production.</summary>
    public void Clear() => _entries.Clear();
}
