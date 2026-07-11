using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Seeded lookup of sites a story can also be posted on (Feature 53 reframe, WU38d —
/// "Also posted on"). <b>Deliberately NOT a hybrid C# enum</b> (settled,
/// audit/Moderation.md F53): nothing branches on platform at compile time, and the fanfic
/// world's long tail of small archives should be seed rows, not code changes. WU39 hangs
/// per-platform verification properties here as columns.
/// </summary>
public partial class ExternalPlatform
{
    public short ExternalPlatformId { get; set; }

    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// Host substring used to auto-detect the platform from a pasted URL in the story form
    /// (e.g. "archiveofourown.org"). Null for the "Other" row, which displays the URL's host.
    /// </summary>
    [MaxLength(256)]
    public string? DomainPattern { get; set; }

    public virtual ICollection<StoryExternalLink> StoryExternalLinks { get; set; } = new List<StoryExternalLink>();
}
