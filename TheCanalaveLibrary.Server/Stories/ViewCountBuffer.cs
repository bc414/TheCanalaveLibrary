using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Singleton in-process coalescing buffer for story view pings (Feature 45 L2 — the
/// signal-buffering pattern, layer2-services.md; sibling of <see cref="ReadingProgressBuffer"/>).
/// Coalescing is a per-story <b>sum</b>: N views of a story in a flush window become one entry.
/// <see cref="ViewCountFlushWorker"/> drains into <c>daily_story_stats</c> (per-story/day
/// accumulation; lifetime total = SUM). Contract: eventually-durable, loss window = one flush
/// interval (views are lossy by design — "a view is a view").
///
/// At N≥2 web nodes this swaps for a shared RESP store (Valkey counters) behind the same
/// <see cref="IViewCountWriteService"/> — a body swap, no interface change.
/// </summary>
public sealed class ViewCountBuffer
{
    private readonly ConcurrentDictionary<int, int> _viewsByStoryId = new();

    // Not IDisposable — same rationale as ReadingProgressBuffer's gauge (shared static Meter;
    // one buffer per production process; test-host duplicates are benign).
    private readonly ObservableGauge<int> _depthGauge;

    public ViewCountBuffer()
    {
        _depthGauge = CanalaveTelemetry.ViewCount.Meter.CreateObservableGauge(
            "canalave.viewcount.buffer.depth",
            () => _viewsByStoryId.Count,
            unit: "{story}",
            description: "Distinct stories with views currently pending flush.");
    }

    /// <summary>Distinct stories with pending views.</summary>
    public int Count => _viewsByStoryId.Count;

    /// <summary>Adds one view for <paramref name="storyId"/> (O(1) coalescing increment).</summary>
    public void Record(int storyId) =>
        _viewsByStoryId.AddOrUpdate(storyId, 1, (_, existing) => existing + 1);

    /// <summary>
    /// Atomically removes and returns all pending (storyId, views) pairs. A ping racing the drain
    /// either increments before removal (this batch) or re-adds afterward (next batch) — never lost.
    /// </summary>
    public List<(int StoryId, int Views)> Drain()
    {
        var drained = new List<(int, int)>(_viewsByStoryId.Count);
        foreach (int storyId in _viewsByStoryId.Keys)
        {
            if (_viewsByStoryId.TryRemove(storyId, out int views))
                drained.Add((storyId, views));
        }
        return drained;
    }

    /// <summary>Adds a previously drained batch back after a failed flush (additive merge).</summary>
    public void Restore(IEnumerable<(int StoryId, int Views)> batch)
    {
        foreach ((int storyId, int views) in batch)
            _viewsByStoryId.AddOrUpdate(storyId, views, (_, existing) => existing + views);
    }

    /// <summary>Discards all pending views. Test isolation only — never called in production.</summary>
    public void Clear() => _viewsByStoryId.Clear();
}
