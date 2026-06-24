namespace TheCanalaveLibrary.Core;

/// <summary>
/// Tracks per-user chapter reading progress (Feature 44). MVP: direct DB writes (L2).
/// L7 swaps the body with a Redis write-behind queue post-MVP.
/// Anonymous viewers are silently ignored — no UserId, no write.
/// </summary>
public interface IReadingProgressWriteService
{
    /// <summary>
    /// Upserts <see cref="UserChapterInteraction"/> for the current viewer:
    /// stamps <c>ReadProgress</c> + <c>LastInteractionDate</c>; flips
    /// <c>IsRead = true</c> once <paramref name="progress"/> ≥ 0.9.
    /// Anonymous viewers no-op.
    /// </summary>
    Task RecordProgressAsync(int chapterId, float progress);
}
