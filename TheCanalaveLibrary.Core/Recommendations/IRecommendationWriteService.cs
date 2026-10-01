namespace TheCanalaveLibrary.Core;

/// <summary>
/// Write side of the Recommendations service contract. Inherits the read interface so callers
/// that need both read and write inject only the narrowest applicable interface.
/// </summary>
public interface IRecommendationWriteService : IRecommendationReadService
{
    /// <summary>
    /// Submits a new recommendation. Sanitizes and validates the body (min
    /// <see cref="RecommendationConstants.MinLength"/> plain-text characters). Publishes
    /// immediately (<c>Approved</c> — WU-RecLifecycle: no pre-publication gate, by design) and
    /// best-effort notifies the story author. Self-recommendation (caller is the story's author)
    /// is rejected. One-per-user-per-story enforced by the DB unique index — duplicate submissions
    /// translate to a friendly validation error.
    /// </summary>
    /// <returns>The new <c>RecommendationId</c>.</returns>
    /// <exception cref="RecommendationValidationException">Body too short, or self-recommendation.</exception>
    /// <exception cref="KeyNotFoundException">Story not found.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated, or a recommendation already exists for this user+story.</exception>
    Task<int> SubmitAsync(RecommendationSubmitDto dto);

    /// <summary>
    /// Edits the body of an existing recommendation. Author-only. Re-sanitizes and re-validates.
    /// Editing a <c>NeedsRevision</c> recommendation automatically returns it to <c>Approved</c>
    /// (revision note cleared; story author best-effort notified). Refused on <c>Rejected</c>
    /// recommendations — a removal is sticky until the story author unblocks it.
    /// </summary>
    /// <exception cref="RecommendationValidationException">Body too short.</exception>
    /// <exception cref="KeyNotFoundException">Recommendation not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not the recommendation's author, or the recommendation is Rejected.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task EditAsync(UpdateRecommendationDto dto);

    /// <summary>
    /// Hard-deletes a recommendation. Author-only. Cascade deletes all
    /// <c>RecommendationLike</c> and <c>RecommendationDetail</c> rows. Refused on <c>Rejected</c>
    /// recommendations — the persisted Rejected row is the block record that keeps the
    /// one-per-user-per-story slot occupied (WU-RecLifecycle stickiness).
    /// </summary>
    /// <exception cref="KeyNotFoundException">Recommendation not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not the recommendation's author, or the recommendation is Rejected.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task DeleteAsync(int recommendationId);

    /// <summary>
    /// Story-author action (WU-RecLifecycle "Correct" path): sends a recommendation back for
    /// revision with a required note. From <c>Approved</c> or <c>NeedsRevision</c> (repeat request
    /// overwrites the note). Sets <c>NeedsRevision</c> (publicly hidden), clears
    /// <c>IsHiddenGem</c>/<c>IsHighlightedByAuthor</c> (flag invariant — slots freed, not
    /// auto-restored), and best-effort notifies the recommender
    /// (<c>RecommendationRevisionRequested</c>). Not sticky — the recommender's edit auto-returns
    /// the recommendation to <c>Approved</c>.
    /// </summary>
    /// <exception cref="RecommendationValidationException">Note empty or too long.</exception>
    /// <exception cref="KeyNotFoundException">Recommendation not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not the story's author, or the recommendation is Rejected.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task RequestRevisionAsync(int recommendationId, string note);

    /// <summary>
    /// Story-author action (WU-RecLifecycle "Remove" path): removes a recommendation from the
    /// story. From <c>Approved</c> or <c>NeedsRevision</c> → <c>Rejected</c>. Silent (no
    /// notification), publicly hidden, and sticky: the recommender cannot edit, delete, or
    /// resubmit — only <see cref="UnblockAsync"/> reverses it. Clears the revision note and both
    /// curation flags, and deletes every reader's attribution naming it in the same save (owner
    /// ruling D3, trigger 5 — a recommendation that can't be displayed can't be reminded or credited;
    /// unblocking does not restore them).
    /// </summary>
    /// <exception cref="KeyNotFoundException">Recommendation not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not the story's author.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task RemoveAsync(int recommendationId);

    /// <summary>
    /// Story-author action: reverses a mistaken/reconsidered <see cref="RemoveAsync"/>. From
    /// <c>Rejected</c> only → straight to <c>Approved</c> (live). Best-effort notifies the
    /// recommender (<c>RecommendationApproved</c> — its only trigger). Curation flags are NOT
    /// restored (re-designate manually).
    /// </summary>
    /// <exception cref="KeyNotFoundException">Recommendation not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not the story's author.</exception>
    /// <exception cref="InvalidOperationException">Recommendation is not Rejected, or caller is not authenticated.</exception>
    Task UnblockAsync(int recommendationId);

    /// <summary>
    /// Toggles a like on a recommendation. Returns the updated denormalized count and the
    /// caller's new like state. No notification — anti-addictive design (§6.11).
    /// A new like requires the recommendation's story to be visible to the caller; an unlike (the
    /// caller already holds a like row) is a clear and always succeeds, even when the story is now
    /// hidden (owner ruling D6). The response is a read and stays gated: an unlike under a hidden
    /// story returns <c>(LikeCount: 0, IsLiked: false)</c>.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Recommendation not found, or (liking only) its story is not visible to the caller.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task<RecommendationLikeResultDto> ToggleLikeAsync(int recommendationId);

    /// <summary>
    /// Sets or clears the Hidden Gem designation on a recommendation. Recommender-only.
    /// Setting to <c>true</c> rejects when the caller already has
    /// <see cref="RecommendationConstants.MaxHiddenGemsPerUser"/> active Hidden Gems (reject-at-limit,
    /// no auto-evict). On successful set, fires a best-effort
    /// <see cref="INotificationWriteService.NotifyStoryHiddenGemAsync"/> to the story author.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Recommendation not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not the recommendation's author.</exception>
    /// <exception cref="InvalidOperationException">Caller already holds the maximum Hidden Gems, or caller is not authenticated.</exception>
    Task SetHiddenGemAsync(int recommendationId, bool isHiddenGem);

    /// <summary>
    /// Sets or clears the story author's spotlight on a recommendation. Story-author-only.
    /// Setting to <c>true</c> rejects when the story already has
    /// <see cref="RecommendationConstants.MaxHighlightedPerStory"/> spotlighted recommendations.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Recommendation not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not the story's author.</exception>
    /// <exception cref="InvalidOperationException">Story already has the maximum spotlighted count, or caller is not authenticated.</exception>
    Task SetHighlightedByAuthorAsync(int recommendationId, bool isHighlighted);

    /// <summary>
    /// The helpful prompt's <b>Yes</b> (spec §5.6, owner ruling D3): records that the recommendation
    /// helped the caller find the story — <c>SuccessfulRecCount</c> +1 and the recommender's badge
    /// credit — and consumes the caller's attribution in the same save, so the prompt never returns.
    /// <b>Requires the caller's attribution for this recommendation</b> (the service audit's §2.4.1
    /// credit-faucet fix): without one, or when the recommendation is not <c>Approved</c> or is taken
    /// down, it throws <see cref="KeyNotFoundException"/> and nothing changes. An already-recorded
    /// success is an idempotent no-op that still clears a lingering attribution row.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Recommendation not found or not visible, or the caller
    /// holds no attribution for it — indistinguishable on purpose.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task RecordSuccessAsync(int recommendationId);

    /// <summary>
    /// The helpful prompt's <b>X</b> — the decline (owner ruling D3): deletes the caller's attribution
    /// naming <paramref name="recommendationId"/> so the prompt never returns. A clear of the caller's
    /// own row, so no visibility guard runs (D6); a no-op when no such row exists.
    /// </summary>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task DismissHelpfulPromptAsync(int recommendationId);
}
