namespace TheCanalaveLibrary.Core;

/// <summary>
/// The categories of abuse-prone user writes that <see cref="IWriteRateLimitService"/> throttles
/// per user. Each kind has its own token-bucket limits (security.md §"Write Throttling" holds the
/// limits table, and its unthrottled-surface enumeration names every user-reachable write that has
/// no bucket, with the reason — owner ruling D20). Not DB-stored — a plain in-process enum, no
/// lookup table; new members are appended so the ordinals stay stable.
/// </summary>
public enum WriteActionKind
{
    /// <summary>Comment posts, all four contexts (chapter / blog post / group / profile wall).</summary>
    Comment,

    /// <summary>Private-message sends and conversation starts.</summary>
    Message,

    /// <summary>User-submitted moderation reports (never the moderator's own actions).</summary>
    Report,

    /// <summary>
    /// Content creates: story, chapter and alternate-version creates; blog-post creates (profile,
    /// group, site); group create; poll creates (site, blog-post); recommendation submit; and, since
    /// WU-ThrottleCoverage (owner ruling D20), custom-list create and clone, series create and
    /// story-arc create.
    /// </summary>
    ContentCreate,

    /// <summary>Image uploads (covers + profile pictures) via <c>ImageUploadProcessor</c>.</summary>
    ImageUpload,

    /// <summary>
    /// External-account and per-link verification requests (Feature 53, WU39) —
    /// <c>SubmitAccountForVerificationAsync</c> and <c>RequestLinkVerificationAsync</c>. Moderator
    /// approve/reject actions are NOT throttled (mod actions are deliberately unthrottled per
    /// security.md).
    /// </summary>
    VerificationRequest,

    /// <summary>
    /// Signed-in mature-content reveals (owner ruling D20) — <c>ContentRevealRecorder.RecordAsync</c>,
    /// the one enforcement point behind the full-document <c>POST /content-gate/reveal</c>. The
    /// anonymous reveal writes only the viewer's own cookie and is exempt.
    /// </summary>
    Reveal,

    /// <summary>
    /// Acknowledgment and lineage requests that notify another user (owner ruling D20) —
    /// <c>RequestAcknowledgmentAsync</c> on every request, <c>RequestLineageAsync</c> unless the link is
    /// self-owned (it auto-approves and notifies nobody). The bucket charges first requests too: a
    /// revoke or delete followed by a fresh request leaves no trace of the repeat, so the service
    /// cannot tell the two apart. The one kind whose call sits after validation rather than at method
    /// entry, because what it guards — the notification — is only known there (security.md).
    /// </summary>
    RepeatRequest,

    /// <summary>
    /// Poll votes (owner ruling D20) — every <c>VoteAsync</c> call: a vote, a change and a retraction
    /// are one method. Poll <em>creation</em> is <see cref="ContentCreate"/>.
    /// </summary>
    Vote
}
