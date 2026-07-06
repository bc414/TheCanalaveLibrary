namespace TheCanalaveLibrary.Core;

/// <summary>
/// Records story views (Feature 45). <b>Durability contract: eventually-durable.</b> Views are a
/// loss-tolerant signal — "a view is a view", no per-user tracking — buffered in-process and
/// batch-flushed into <c>daily_story_stats</c> (per-story/day; lifetime total = SUM). The last few
/// seconds of views can be lost on a hard crash. Anonymous viewers count (deliberately no auth gate).
/// Never a sort key anywhere — views are a non-sortable, on-demand informational metric
/// (anti-popularity-snowball philosophy; see DefaultSortOrder's exclusion note).
/// </summary>
public interface IViewCountWriteService
{
    /// <summary>
    /// Counts one view of <paramref name="storyId"/>. Fired by the story page's first client ping
    /// (5-second timer or first scroll, whichever comes first) — never on raw page load, which
    /// filters bots and bounces.
    /// </summary>
    Task RecordViewAsync(int storyId);
}
