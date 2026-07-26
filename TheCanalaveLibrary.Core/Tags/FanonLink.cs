using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// The fanonize act's persistent record (WU-TagFanon Group 7): a cross-author custom-name group —
/// identified by <see cref="NormalizedName"/> (case-insensitive, trimmed) on
/// <see cref="BaseTagId"/> — pointed at an official <see cref="TargetTagId"/> the affected
/// authors are invited to adopt. The link is ongoing, not one-shot: the dashboard shows adoption
/// state through it, moderators notify newly-arrived authors through it, and the editor nudge
/// resolves through the same normalization. The target may be fanon OR canon (an OC name turning
/// out to be a canon character is the same correction shape). Unique per (name, base tag).
/// </summary>
public partial class FanonLink
{
    public int FanonLinkId { get; set; }

    /// <summary>Lower-cased, trimmed custom name — the group key (same rule as the dashboard
    /// grouping and the editor nudge match).</summary>
    [Required]
    [MaxLength(128)]
    public string NormalizedName { get; set; } = null!;

    /// <summary>The group's base/archetype tag (a Character species, a Setting, …).</summary>
    public int BaseTagId { get; set; }

    /// <summary>The official tag authors are invited to adopt.</summary>
    public int TargetTagId { get; set; }

    /// <summary>Moderator who made the link (SET NULL on user deletion).</summary>
    public int? LinkedByUserId { get; set; }

    public DateTime DateLinked { get; set; }

    public virtual Tag BaseTag { get; set; } = null!;

    public virtual Tag TargetTag { get; set; } = null!;

    public virtual User? LinkedByUser { get; set; }
}
