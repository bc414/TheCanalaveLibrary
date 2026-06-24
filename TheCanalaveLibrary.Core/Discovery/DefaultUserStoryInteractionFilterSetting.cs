using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// System default matrix: (SearchModeKey × UserStoryInteractionFilterKey) → IsEnabled.
/// One row per (mode, filter-kind) pair. Sparse per-user overrides live in
/// <see cref="UserStoryInteractionFilterSetting"/>.
/// </summary>
public partial class DefaultUserStoryInteractionFilterSetting
{
    [Required]
    [MaxLength(50)]
    public string SearchModeKey { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string UserStoryInteractionFilterKey { get; set; } = null!;

    public bool IsEnabled { get; set; }

    public virtual UserStoryInteractionFilterType UserStoryInteractionFilterType { get; set; } = null!;

    public virtual SearchMode SearchModeKeyNavigation { get; set; } = null!;
}
