namespace TheCanalaveLibrary.Core;

/// <summary>
/// Tracks per-user chapter reading progress (Feature 44). <b>Durability contract:
/// eventually-durable.</b> Progress is a loss-tolerant signal — writes may be buffered and
/// batch-flushed, so the last few seconds of pings can be lost on a hard crash and are not
/// read-your-own-write visible until a flush. Callers must not treat a completed call as
/// persisted. Deliberate, durable actions (e.g. HasStarted at 90% of Chapter 1) go through
/// IUserStoryInteractionWriteService instead — never this seam.
/// Anonymous viewers are silently ignored — no UserId, no write.
/// </summary>
public interface IReadingProgressWriteService
{
    /// <summary>
    /// Records a progress ping for the current viewer on <paramref name="chapterId"/>: the
    /// persisted row keeps the high-water <c>ReadProgress</c> + latest <c>LastInteractionDate</c>,
    /// and flips <c>IsRead = true</c> once <paramref name="progress"/> ≥ 0.9 (never auto-unset).
    /// Anonymous viewers no-op.
    /// </summary>
    Task RecordProgressAsync(int chapterId, float progress);
}
