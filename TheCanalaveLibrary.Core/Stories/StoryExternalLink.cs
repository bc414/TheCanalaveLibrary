using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// One "Also posted on" link — a story may list every external site it's also live on
/// (Feature 53 reframe, WU38d; remodeled from the old single-row <c>StoryImport</c>).
/// Primary use is story-page display (hence <c>Core/Stories/</c>); each link carries an
/// author-verification state whose checkmark is the community's anti-theft signal
/// (unverified links on a recognized story → report it, Feature 46). Moderator verification
/// workflow = WU39.
/// </summary>
public partial class StoryExternalLink
{
    [Key]
    public int StoryExternalLinkId { get; set; }

    public int StoryId { get; set; }

    public short ExternalPlatformId { get; set; }

    [Required]
    [MaxLength(2048)]
    public string Url { get; set; } = null!;

    public VerificationStatusEnum VerificationStatus { get; set; }

    public DateTime DateAdded { get; set; }

    /// <summary>
    /// Per-link tier (WU39). Null = not requested. Set when the author requests review — but only
    /// once their <see cref="UserExternalIdentity"/> account tier is Verified for this platform;
    /// see <c>ServerExternalVerificationWriteService.RequestLinkVerificationAsync</c>. The
    /// existing delete+add resync on URL edit (<c>ServerStoryWriteService</c>) already clears this
    /// on re-add, same as it resets <see cref="VerificationStatus"/>.
    /// </summary>
    public DateTime? DateVerificationRequested { get; set; }

    /// <summary>Author-facing only — shown in the editor so they can fix and re-request. Never projected to the reader page.</summary>
    [MaxLength(512)]
    public string? RejectionReason { get; set; }

    public virtual Story Story { get; set; } = null!;

    public virtual ExternalPlatform ExternalPlatform { get; set; } = null!;
}
