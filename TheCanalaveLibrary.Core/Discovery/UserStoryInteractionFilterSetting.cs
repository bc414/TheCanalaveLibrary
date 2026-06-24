using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Sparse per-user override of the system default matrix.
/// One row per (User × SearchMode × filter-kind) when the user's preference differs from
/// <see cref="DefaultUserStoryInteractionFilterSetting"/>.
/// </summary>
public partial class UserStoryInteractionFilterSetting
{
    public int UserStoryInteractionFilterSettingId { get; set; }

    public int UserId { get; set; }

    [Required]
    [MaxLength(50)]
    public string SearchModeKey { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string UserStoryInteractionFilterKey { get; set; } = null!;

    public bool IsEnabled { get; set; }

    public virtual UserStoryInteractionFilterType UserStoryInteractionFilterType { get; set; } = null!;

    public virtual SearchMode SearchModeKeyNavigation { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
