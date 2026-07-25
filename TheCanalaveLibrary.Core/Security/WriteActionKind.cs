namespace TheCanalaveLibrary.Core;

/// <summary>
/// The categories of abuse-prone user writes that <see cref="IWriteRateLimitService"/> throttles
/// per user. Each kind has its own token-bucket limits (see security.md §"Write Throttling" for
/// the limits table and for what is deliberately unthrottled — bounded toggles, edits, mod
/// actions). Not DB-stored — a plain in-process enum, no lookup table.
/// </summary>
public enum WriteActionKind
{
    /// <summary>Comment posts, all four contexts (chapter / blog post / group / profile wall).</summary>
    Comment,

    /// <summary>Private-message sends and conversation starts.</summary>
    Message,

    /// <summary>User-submitted moderation reports (never the moderator's own actions).</summary>
    Report,

    /// <summary>Story / chapter / alternate-version / blog-post creates, recommendation submits.</summary>
    ContentCreate,

    /// <summary>Image uploads (covers + profile pictures) via <c>ImageUploadProcessor</c>.</summary>
    ImageUpload,

    /// <summary>
    /// External-account and per-link verification requests (Feature 53, WU39) —
    /// <c>SubmitAccountForVerificationAsync</c> and <c>RequestLinkVerificationAsync</c>. Moderator
    /// approve/reject actions are NOT throttled (mod actions are deliberately unthrottled per
    /// security.md).
    /// </summary>
    VerificationRequest
}
