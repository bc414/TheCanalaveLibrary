namespace TheCanalaveLibrary.Core;

/// <summary>
/// Write side of the Story Acknowledgments service contract (WU-StatBadgeProducers). Every method
/// requires an authenticated user. Ownership rule: requesting/revoking requires owning the
/// <b>story</b> (the crediting author); accepting/declining requires <b>being</b> the credited user.
/// </summary>
public interface IStoryAcknowledgmentWriteService : IStoryAcknowledgmentReadService
{
    /// <summary>
    /// Requests a new credit. Caller must own <see cref="CreateStoryAcknowledgmentDto.StoryId"/> —
    /// throws <see cref="UnauthorizedAccessException"/> otherwise. Throws
    /// <see cref="StoryAcknowledgmentValidationException"/> on invalid input, an unknown recipient,
    /// self-crediting, or an unknown role id.
    ///
    /// <para><b>Always consent-gated:</b> unlike <c>IStoryLineageWriteService</c>'s self-owned-target
    /// auto-approve, a credit always starts <see cref="StoryAcknowledgmentStatus.Pending"/> — there
    /// is no "self-owned" case, since crediting yourself is rejected outright.</para>
    ///
    /// <para><b>Re-request after decline:</b> the composite key is
    /// <c>(StoryId, AcknowledgedUserId, AcknowledgmentRoleId)</c> — a prior
    /// <see cref="StoryAcknowledgmentStatus.Declined"/> row for the same triple is updated back to
    /// Pending, not duplicate-inserted.</para>
    /// </summary>
    Task RequestAcknowledgmentAsync(CreateStoryAcknowledgmentDto dto);

    /// <summary>
    /// Accepts a Pending credit naming the caller as recipient. Unlike <see cref="RevokeAsync"/>,
    /// the recipient id is NOT a parameter — it is always the authenticated caller, so there is no
    /// id to validate or spoof (a stronger shape than passing the id and checking it matches).
    /// Throws <see cref="KeyNotFoundException"/> if no such credit exists; throws
    /// <see cref="StoryAcknowledgmentValidationException"/> if it is not Pending. For role Beta
    /// Reader, increments <c>UserStat.AcknowledgedAsBetaReaderCount</c> and checks the
    /// <c>BetaReader</c> badge threshold (≥1), best-effort.
    /// </summary>
    Task AcceptAsync(int storyId, short roleId);

    /// <summary>
    /// Declines a Pending credit naming the caller as recipient (kept as a
    /// <see cref="StoryAcknowledgmentStatus.Declined"/> row, not deleted — allows a later re-credit
    /// to reuse the row). Same caller-is-recipient shape as <see cref="AcceptAsync"/>. No counter
    /// change — a Pending credit was never counted.
    /// </summary>
    Task DeclineAsync(int storyId, short roleId);

    /// <summary>
    /// Removes a credit the caller authored (any status), deleting the row entirely — a revoke is
    /// the author retracting the claim, not a decision the recipient made, so there is no
    /// re-request-reuses-the-row history to preserve. Caller must own the story. If the credit was
    /// <see cref="StoryAcknowledgmentStatus.Accepted"/> at the time (role Beta Reader), decrements
    /// <c>UserStat.AcknowledgedAsBetaReaderCount</c> — the transition-delta rule
    /// (<c>layer2-services.md</c>). Idempotent — a no-op if the credit doesn't exist.
    /// </summary>
    Task RevokeAsync(int storyId, int acknowledgedUserId, short roleId);
}
