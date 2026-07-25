namespace TheCanalaveLibrary.Core;

// DTOs for Feature 53's WU39 verification workflow (settled 2026-07-24, audit/Moderation.md F53).
// Two tiers: account (UserExternalIdentity) and per-link (StoryExternalLink.VerificationStatus,
// reused from WU38d).

/// <summary>Author's own account-tier status for one platform — the Settings "External accounts" surface.</summary>
public record ExternalAccountDto(
    short ExternalPlatformId,
    string PlatformName,
    string ProfileUrl,
    string Handle,
    VerificationStatusEnum Status,
    string? RejectionReason);

/// <summary>A platform offering verification (<c>SupportsVerification == true</c>), with its placement instructions, for the Settings form.</summary>
public record VerificationPlatformDto(short ExternalPlatformId, string Name, string? PlacementInstructions);

/// <summary>Submits (or re-submits after rejection) an external account for account-tier review.</summary>
public record AddExternalAccountRequest(short ExternalPlatformId, string ProfileUrl, string Handle);

/// <summary>One pending account-tier request in the moderator queue — the mod opens <see cref="ProfileUrl"/> and looks for <see cref="VerificationCode"/>.</summary>
public record PendingAccountVerificationDto(
    int UserExternalIdentityId,
    int UserId,
    string UserName,
    short ExternalPlatformId,
    string PlatformName,
    string ProfileUrl,
    string Handle,
    string VerificationCode,
    DateTime DateRequested);

/// <summary>One pending per-link request in the moderator queue — the mod opens <see cref="StoryUrl"/> or <see cref="LinkUrl"/> and compares the linked story's author to <see cref="AccountHandle"/>.</summary>
public record PendingLinkVerificationDto(
    int StoryExternalLinkId,
    int StoryId,
    string StoryTitle,
    string StoryUrl,
    short ExternalPlatformId,
    string PlatformName,
    string LinkUrl,
    int AuthorUserId,
    string AuthorUserName,
    string AccountHandle,
    string AccountProfileUrl,
    DateTime DateRequested);

/// <summary>Author-facing per-link status, derived (never stored) from <see cref="VerificationStatusEnum"/> + the requested flag.</summary>
public enum LinkVerificationDisplayStatus
{
    NotRequested,
    PendingReview,
    Confirmed,
    Rejected,
}

/// <summary>
/// Pure derivation of the author-facing per-link status label — unit-testable, no DbContext.
/// Mirrors <see cref="PublicVerificationCode"/>/<see cref="StorySlug"/> as a Core-only helper.
/// </summary>
public static class LinkVerificationStatusHelper
{
    public static LinkVerificationDisplayStatus GetDisplayStatus(VerificationStatusEnum status, bool verificationRequested)
    {
        return status switch
        {
            VerificationStatusEnum.Verified => LinkVerificationDisplayStatus.Confirmed,
            VerificationStatusEnum.Rejected => LinkVerificationDisplayStatus.Rejected,
            _ => verificationRequested ? LinkVerificationDisplayStatus.PendingReview : LinkVerificationDisplayStatus.NotRequested,
        };
    }
}
