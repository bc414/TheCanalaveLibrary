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
    ///   <item><b>Guards raises only (owner ruling D6):</b> if any bit goes false→true (with no row,
    ///   every true bit), the story must be visible to the caller, else
    ///   <see cref="KeyNotFoundException"/> and nothing is written — a mixed payload is refused
    ///   whole. Clears on the caller's existing row, including the all-false delete, always succeed,
    ///   even when the story is now hidden (rating, status or takedown). No row + all-false is a
    ///   silent no-op for any story id, real or not.</item>
    ///   <item><b>Attribution removal (owner ruling D3, trigger 1):</b> when <c>IsReadItLater</c> goes
    ///   true→false, the viewer's recommendation attribution for the story is deleted in the same unit
    ///   of work — the attribution describes how that bit was set, so it dies with it.</item>
    /// </list>
    /// Throws <see cref="InvalidOperationException"/> when the viewer is anonymous.
    /// </summary>
    Task SetUserStoryInteractionStateAsync(int storyId, UserStoryInteractionStateUpdate update);

    /// <summary>
    /// The recommendation card's Read It Later button (Feature 30's durable entry point, owner ruling
    /// D3). In <b>one</b> unit of work: upserts the viewer's interaction row for the recommendation's
    /// story with <c>IsReadItLater = true</c> (stamping <c>ReadItLaterDate</c> once; every other bit
    /// untouched — unlike <see cref="SetUserStoryInteractionStateAsync"/>'s six-bit absolute set) and,
    /// when this call flipped the bit false→true, records the recommendation as the attribution — unless
    /// one already exists (first wins) or the viewer is the story's author (the save still happens).
    /// A raise, so the full story-visibility guard applies (D6).
    /// </summary>
    /// <exception cref="InvalidOperationException">The viewer is anonymous (the UI nudges to log in first).</exception>
    /// <exception cref="KeyNotFoundException">The recommendation does not exist, is not <c>Approved</c>,
    /// is taken down, or its story is not visible to the viewer — indistinguishable on purpose.</exception>
    Task SetReadItLaterFromRecommendationAsync(int recommendationId);

    /// <summary>
    /// Idempotent upsert that flips <c>HasStarted = true</c> for the current viewer on
    /// <paramref name="storyId"/>. Called by the reading page when Ch.1 reaches ≥90% scroll
    /// (WU26). Never clears other interaction flags. Anonymous viewers are silently ignored.
    /// On a genuine false→true flip (and only when not already completed), also applies the
    /// <c>StoriesInProgress</c> transition-delta (A3, 2026-07-24) — this is the sole real-time
    /// producer of that counter's increment; <see cref="MarkCompletedAsync"/>'s decrement depends
    /// on it having run first.
    /// <para><b>Direct-link attribution (owner ruling D3):</b> <paramref name="attributedRecommendationId"/>
    /// is the <c>?rec=</c> a reader arrived with. In the same save, it becomes the viewer's attribution
    /// for the story when none exists yet and the recommendation is attributable (belongs to this story,
    /// <c>Approved</c>, not taken down, viewer not the story's author). The URL is untrusted and this is
    /// the reading path's primary write, so an unattributable value is <b>silently ignored</b> — it never
    /// makes this method throw.</para>
    /// </summary>
    Task MarkStartedAsync(int storyId, int? attributedRecommendationId = null);

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
