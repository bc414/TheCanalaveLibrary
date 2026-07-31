using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

public partial class UserBadge
{
    public int UserId { get; set; }

    [Required]
    [MaxLength(128)]
    public string BadgeKey { get; set; } = null!;

    public DateTime DateEarned { get; set; }

    public int DisplayOrder { get; set; }

    /// <summary>
    /// The producing counter's value at last award/drift-correction (WU-StatBadgeProducers — the
    /// no-tiers model: a badge is earned at ≥1 and displays this count instead of a Bronze/Silver
    /// split). Written by the same producer call that bumps the backing <c>UserStat</c> counter
    /// (<see cref="IBadgeWriteService.AwardAsync"/>), and drift-corrected by
    /// <c>UserStatRecalculator</c>'s third pass so the two never diverge.
    /// </summary>
    public int EarnedCount { get; set; }

    public virtual Badge BadgeKeyNavigation { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
