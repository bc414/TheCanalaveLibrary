using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// The account tier of Feature 53's two-tier verification model (WU39, settled 2026-07-24,
/// audit/Moderation.md F53). One row per (user, platform) — a durable fact, established once and
/// reused across every story that user links on that platform: "TCL user U controls external
/// profile P on platform X."
///
/// The user places their own site-wide public <see cref="User.VerificationCode"/> on
/// <see cref="ProfileUrl"/> (placement surface is data-driven — see
/// <see cref="ExternalPlatform.PlacementInstructions"/>, never a code branch); a moderator opens
/// the profile and confirms the code is present, then flips <see cref="VerificationStatus"/>. No
/// server-side fetch, ever — manual review only.
///
/// This tier alone does NOT prove authorship of any specific linked story (platform work URLs
/// don't name their author) — see <see cref="StoryExternalLink.VerificationStatus"/> for the
/// per-link tier that does.
/// </summary>
public partial class UserExternalIdentity
{
    [Key]
    public int UserExternalIdentityId { get; set; }

    public int UserId { get; set; }

    public short ExternalPlatformId { get; set; }

    [Required]
    [MaxLength(2048)]
    public string ProfileUrl { get; set; } = null!;

    /// <summary>The author's actual handle on that platform — shown to readers on a reviewed link.</summary>
    [Required]
    [MaxLength(128)]
    public string Handle { get; set; } = null!;

    public VerificationStatusEnum VerificationStatus { get; set; }

    public DateTime DateRequested { get; set; }

    public DateTime? DateReviewed { get; set; }

    public int? ReviewedByModeratorUserId { get; set; }

    /// <summary>Author-facing only — shown in Settings so they can fix and re-request.</summary>
    [MaxLength(512)]
    public string? RejectionReason { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual User? ReviewedByModeratorUser { get; set; }

    public virtual ExternalPlatform ExternalPlatform { get; set; } = null!;
}
