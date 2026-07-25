namespace TheCanalaveLibrary.Core;

/// <summary>
/// Durable manual read-marks (WU45 — the Fimfiction-style per-row toggle + mark-all). This is
/// <b>deliberate user intent</b>, so it is a separate, durable-direct seam from the buffered
/// <see cref="IReadingProgressWriteService"/> (whose contract is loss-tolerant scroll pings —
/// the signal-buffering criterion in layer2-services.md). Never route manual marks through the
/// buffer.
///
/// <para><b>Both fields move together (WU45 settled):</b> mark-read sets
/// <c>IsRead = true, ReadProgress = 1</c>; mark-unread sets <c>false, 0</c>. Required because the
/// flush pipeline recomputes <c>is_read = progress ≥ 0.9</c> from high-water progress — leaving a
/// stale fraction behind would let the next flush silently resurrect the overridden state. The
/// implementation also discards any pending buffered ping for the affected chapters.</para>
///
/// <para>Mark-read additionally flips the story's <c>HasStarted</c> via the existing idempotent
/// <c>MarkStartedAsync</c> ("read it elsewhere" case); mark-unread never un-sets it (permanent
/// past event). Anonymous callers throw — the UI gates these controls behind AuthorizeView.</para>
///
/// <para><b>A3 (2026-07-24):</b> mark-read additionally calls the idempotent
/// <c>MarkCompletedAsync</c> when the marked chapter is the story's last published chapter and
/// <c>Story.StoryStatusId == StoryStatusEnum.Completed</c> — the manual-path half of the
/// story-completion auto-producer (layer2-services.md §"<c>IsCompleted</c> auto-producer"). Never
/// fires for ongoing stories; mark-unread never un-sets <c>IsCompleted</c>.</para>
/// </summary>
public interface IChapterReadMarkWriteService
{
    /// <exception cref="KeyNotFoundException">Chapter not found.</exception>
    /// <exception cref="InvalidOperationException">Anonymous caller.</exception>
    Task SetChapterReadAsync(int chapterId, bool isRead);

    /// <summary>
    /// Marks every <b>published</b> chapter of the story read (creating missing interaction rows)
    /// or unread (flipping existing rows only — absent rows are already unread; sparse semantics).
    /// </summary>
    /// <exception cref="KeyNotFoundException">Story not found.</exception>
    /// <exception cref="InvalidOperationException">Anonymous caller.</exception>
    Task SetAllChaptersReadAsync(int storyId, bool isRead);
}
