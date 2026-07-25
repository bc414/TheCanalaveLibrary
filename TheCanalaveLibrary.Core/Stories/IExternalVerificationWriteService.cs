namespace TheCanalaveLibrary.Core;

/// <summary>Write side — see <see cref="IExternalVerificationReadService"/> for the cluster/settled-design context.</summary>
public interface IExternalVerificationWriteService : IExternalVerificationReadService
{
    /// <summary>Lazily gets-or-creates the caller's site-wide public <see cref="User.VerificationCode"/>. Idempotent — a second call returns the same value.</summary>
    Task<string> EnsureMyVerificationCodeAsync();

    /// <summary>
    /// Submits (or re-submits after rejection) an external account for account-tier review.
    /// Upserts the (caller, platform) <see cref="UserExternalIdentity"/> to <c>Unverified</c>,
    /// clearing any prior <c>DateReviewed</c>/<c>RejectionReason</c>. Throws
    /// <see cref="InvalidOperationException"/> if the URL isn't absolute http/https or the
    /// platform doesn't support verification.
    /// </summary>
    Task SubmitAccountForVerificationAsync(AddExternalAccountRequest request);

    /// <summary>
    /// Author-only: requests per-link review for one of the caller's own
    /// <see cref="StoryExternalLink"/> rows. Requires the caller already hold a <c>Verified</c>
    /// account-tier identity for that link's platform — throws
    /// <see cref="InvalidOperationException"/> ("Verify your &lt;platform&gt; account first.")
    /// otherwise.
    /// </summary>
    Task RequestLinkVerificationAsync(int storyExternalLinkId);

    /// <summary>Moderator-only: confirms the code was found on the profile URL, flips the account-tier identity to <c>Verified</c>.</summary>
    Task ApproveAccountVerificationAsync(int userExternalIdentityId);

    /// <summary>Moderator-only: the code was not found (or didn't match) — flips to <c>Rejected</c> with a reason the author can act on.</summary>
    Task RejectAccountVerificationAsync(int userExternalIdentityId, string reason);

    /// <summary>Moderator-only: confirms the linked story's listed author matches the confirmed account handle — flips the link's <see cref="VerificationStatusEnum"/> to <c>Verified</c>.</summary>
    Task ApproveLinkVerificationAsync(int storyExternalLinkId);

    /// <summary>Moderator-only: the linked story's author didn't match — flips to <c>Rejected</c> with a reason. Not hidden publicly (settled — hiding reads as an accusation).</summary>
    Task RejectLinkVerificationAsync(int storyExternalLinkId, string reason);
}
