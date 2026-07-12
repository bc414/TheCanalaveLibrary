namespace TheCanalaveLibrary.Core;

/// <summary>
/// Write side of the Story Lineage service contract (Feature 10, WU42). Every method requires an
/// authenticated user and enforces ownership of the relevant side of the link — requesting/deleting
/// requires owning the <b>source</b> story; approving/rejecting requires owning the <b>target</b>
/// story (see <c>audit/Stories.md</c> Feature 10 settled note).
/// </summary>
public interface IStoryLineageWriteService : IStoryLineageReadService
{
    /// <summary>
    /// Requests a new lineage link. Caller must own <see cref="CreateStoryLineageDto.SourceStoryId"/>
    /// — throws <see cref="UnauthorizedAccessException"/> otherwise. Throws
    /// <see cref="StoryLineageValidationException"/> on invalid input, an unknown target story, or an
    /// unknown type id.
    ///
    /// <para><b>Self-owned targets auto-approve:</b> when the caller also owns the target story, the
    /// link is created already <see cref="StoryLineageStatus.Approved"/> and no notification is sent
    /// (matches the notification drop-self invariant). Otherwise the link starts
    /// <see cref="StoryLineageStatus.Pending"/> and the target author receives a best-effort
    /// <c>StoryLineageRequested</c> notification.</para>
    ///
    /// <para><b>Re-request after rejection:</b> the composite key is
    /// <c>(SourceStoryId, TargetStoryId, RelationshipTypeId)</c> — a prior
    /// <see cref="StoryLineageStatus.Rejected"/> row for the same triple is updated back to
    /// Pending/Approved (per the auto-approve rule above), not duplicate-inserted.</para>
    /// </summary>
    Task RequestLineageAsync(CreateStoryLineageDto dto);

    /// <summary>
    /// Approves a Pending request. Caller must own the <b>target</b> story — throws
    /// <see cref="UnauthorizedAccessException"/> otherwise; throws <see cref="KeyNotFoundException"/>
    /// if no such link exists. Sends a best-effort <c>StoryLineageApproved</c> notification to the
    /// source story's author.
    /// </summary>
    Task ApproveLineageAsync(int sourceStoryId, int targetStoryId, short typeId);

    /// <summary>
    /// Rejects a Pending request (kept as a <see cref="StoryLineageStatus.Rejected"/> row, not
    /// deleted — prevents immediate re-request spam and preserves an audit trail). Caller must own
    /// the <b>target</b> story. No notification (silent rejection, matching the moderation model).
    /// </summary>
    Task RejectLineageAsync(int sourceStoryId, int targetStoryId, short typeId);

    /// <summary>
    /// Removes an outgoing link the caller owns (any status). Caller must own the <b>source</b>
    /// story. Idempotent — a no-op if the link doesn't exist.
    /// </summary>
    Task DeleteLineageAsync(int sourceStoryId, int targetStoryId, short typeId);
}
