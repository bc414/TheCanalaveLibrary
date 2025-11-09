using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core.Models;

/// <summary>
/// Represents a user's override of the default setting, stored for that user for one particular filter and search mode.
/// </summary>
public partial class UserSearchSetting
{
    public int UserSearchSettingId { get; set; }

    public int UserId { get; set; }

    [Required]
    [MaxLength(50)]
    public string SearchModeKey { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string InteractionFilterKey { get; set; } = null!;

    public bool IsEnabled { get; set; }

    /* don't remember why we need this
    public string? Value { get; set; }
    */

    public virtual UserInteractionFilter InteractionFilterKeyNavigation { get; set; } = null!;

    public virtual SearchMode SearchModeKeyNavigation { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
