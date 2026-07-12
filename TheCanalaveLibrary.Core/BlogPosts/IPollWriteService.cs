namespace TheCanalaveLibrary.Core;

/// <summary>
/// Write side of Feature 37 (Polls). Per-context creation methods (site vs blog post) follow the
/// per-context method pattern (<c>layer2-services.md</c> "Group Comments — Per-Context Method
/// Pattern") — the two contexts differ in their authorization check.
///
/// <para><b>Permissions:</b> site polls — moderators/admins only. Blog-post polls — the post's
/// author. Voting — any authenticated user. All gates are service-enforced; UI affordances are
/// convenience only.</para>
///
/// <para><b>Config lock:</b> once any vote exists, <c>AllowMultiple</c>/<c>ResultsVisibility</c>/
/// <c>AnonymityMode</c>/<c>DateOpened</c> are frozen — <see cref="UpdatePollAsync"/> throws
/// <see cref="PollValidationException"/> if they differ. Name/description/options stay editable
/// while open; material edits to a voted-on poll stamp <c>LastEditedAt</c> for the 30-minute
/// quiet-period notification sweep (<c>PollEditNotificationSweeper</c>).</para>
/// </summary>
public interface IPollWriteService : IPollReadService
{
    /// <summary>Creates a site poll. Moderator/admin only.</summary>
    Task<int> CreateSitePollAsync(PollEditDto dto);

    /// <summary>Creates a poll attached to a blog post. Post author only.</summary>
    Task<int> CreateBlogPostPollAsync(int blogPostId, PollEditDto dto);

    /// <summary>
    /// Updates name/description/dates/options (and config fields while unvoted). Closed polls are
    /// not editable. Options are reconciled by id: absent = deleted (votes cascade), null-id = new.
    /// </summary>
    Task UpdatePollAsync(int pollId, PollEditDto dto);

    /// <summary>Manually closes an open poll now (stamps <c>DateClosed = UtcNow</c>).</summary>
    Task ClosePollAsync(int pollId);

    /// <summary>Archives/unarchives a site poll (display-only; orthogonal to closed). Moderator/admin only.</summary>
    Task SetSitePollArchivedAsync(int pollId, bool archived);

    /// <summary>Deletes a poll and (cascade) its options and votes.</summary>
    Task DeletePollAsync(int pollId);

    /// <summary>
    /// Replaces the caller's votes on <paramref name="pollId"/> with <paramref name="optionIds"/>
    /// (empty = retract all). Single-choice polls accept at most one id. Only while Open.
    /// <paramref name="voteAnonymously"/> is honored only when the poll's anonymity mode is
    /// <c>VoterChoice</c>. Returns the refreshed viewer-relative poll.
    /// </summary>
    Task<PollDto> VoteAsync(int pollId, int[] optionIds, bool voteAnonymously);
}
