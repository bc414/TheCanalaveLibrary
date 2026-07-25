namespace TheCanalaveLibrary.Core;

/// <summary>
/// Write side of the UserStoryInteractions feature cluster. Inherits the read side so the panel
/// composite can inject a single interface and still read back state after a debounce flush.
/// </summary>
public interface IUserStoryInteractionWriteService : IUserStoryInteractionReadService
{
    /// <summary>
    /// Consolidated upsert of the six panel-managed bits for the current viewer + story. Called
    /// once per story after the panel's debounce fires, not on every button click. The service:
    /// <list type="bullet">
    ///   <item>Preserves <c>HasStarted</c> (read-path owned, WU26).</item>
    ///   <item>Stamps / nulls <see cref="UserStoryInteractionDate"/> columns per spec §4.</item>
    ///   <item>Removes the row entirely when all seven bits are false (sparse semantics).</item>
    ///   <item>Accepts every combination — spec §4's zero-coupling model forbids nothing
    ///   (ValidateCombination is an empty extension point for future restrictions).</item>
    /// </list>
    /// Throws <see cref="InvalidOperationException"/> when the viewer is anonymous.
    /// </summary>
    Task SetUserStoryInteractionStateAsync(int storyId, UserStoryInteractionStateUpdate update);

    /// <summary>
    /// Idempotent upsert that flips <c>HasStarted = true</c> for the current viewer on
    /// <paramref name="storyId"/>. Called by the reading page when Ch.1 reaches ≥90% scroll
    /// (WU26). Never clears other interaction flags. Anonymous viewers are silently ignored.
    /// On a genuine false→true flip (and only when not already completed), also applies the
    /// <c>StoriesInProgress</c> transition-delta (A3, 2026-07-24) — this is the sole real-time
    /// producer of that counter's increment; <see cref="MarkCompletedAsync"/>'s decrement depends
    /// on it having run first.
    /// </summary>
    Task MarkStartedAsync(int storyId);

    /// <summary>
    /// Idempotent upsert that flips <c>IsCompleted = true</c> for the current viewer on
    /// <paramref name="storyId"/> — the application-side producer for spec §5.12 (A3, 2026-07-24).
    /// Mirrors <see cref="MarkStartedAsync"/>: a durable direct write, never routed through the
    /// reading-progress signal buffer. Callers gate invocation to Completed stories only (an ongoing
    /// story's "caught up" state stays the existing query-time computation — never auto-set here);
    /// see <c>layer2-services.md</c> §"<c>IsCompleted</c> auto-producer" for the full design.
    /// No-op if the row is already <c>IsCompleted</c> (no double counter increment) or if the
    /// viewer is anonymous. Never clears other interaction flags, and never auto-clears
    /// <c>IsCompleted</c> once set.
    /// </summary>
    Task MarkCompletedAsync(int storyId);
}
