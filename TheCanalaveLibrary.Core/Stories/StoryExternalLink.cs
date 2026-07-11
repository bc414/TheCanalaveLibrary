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

    public virtual Story Story { get; set; } = null!;

    public virtual ExternalPlatform ExternalPlatform { get; set; } = null!;
}
