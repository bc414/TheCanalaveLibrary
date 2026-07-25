namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read side of Feature 53's WU39 verification workflow (settled 2026-07-24,
/// audit/Moderation.md F53). Colocated in the Stories cluster — not Moderation — per the
/// <c>ISpotlightSlotAllocator</c> precedent (a mod-gated feature service lives with its feature,
/// not folded into <see cref="IModerationReadService"/>); only the mod review tab UI lives in
/// Moderation. CQRS-lite: write inherits read.
/// </summary>
public interface IExternalVerificationReadService
{
    /// <summary>Platforms offering verification (<c>SupportsVerification == true</c>) with their placement instructions — feeds the Settings "External accounts" form.</summary>
    Task<IReadOnlyList<VerificationPlatformDto>> GetVerificationPlatformsAsync();

    /// <summary>The caller's own account-tier status per platform.</summary>
    Task<IReadOnlyList<ExternalAccountDto>> GetMyExternalAccountsAsync();

    /// <summary>
    /// Moderator queue — pending account-tier requests (<c>VerificationStatus == Unverified</c>).
    /// An elevated work-surface read (M-content-agnostic, like <c>ServerModerationReadService</c>) —
    /// mods see every pending request regardless of their own content-rating setting.
    /// </summary>
    Task<IReadOnlyList<PendingAccountVerificationDto>> GetPendingAccountVerificationsAsync();

    /// <summary>
    /// Moderator queue — pending per-link requests: <c>VerificationStatus == Unverified</c>,
    /// <c>DateVerificationRequested != null</c>, AND the story's author holds a <c>Verified</c>
    /// account-tier identity for that link's platform (no per-link item exists before the account
    /// tier is proven).
    /// </summary>
    Task<IReadOnlyList<PendingLinkVerificationDto>> GetPendingLinkVerificationsAsync();
}
