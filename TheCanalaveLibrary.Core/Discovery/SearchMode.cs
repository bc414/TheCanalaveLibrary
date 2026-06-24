using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// A SearchMode represents a discovery surface on the site (e.g. SearchPage, TreeSearch).
/// Each mode can have a different set of default filter criteria and per-user overrides.
/// </summary>
public partial class SearchMode
{
    [Key]
    [Required]
    [MaxLength(50)]
    public string SearchModeKey { get; set; } = null!;

    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = null!;

    [MaxLength(2048)]
    public string? Description { get; set; }

    public virtual ICollection<DefaultUserStoryInteractionFilterSetting> DefaultUserStoryInteractionFilterSettings { get; set; } = new List<DefaultUserStoryInteractionFilterSetting>();

    public virtual ICollection<UserCustomFilter> UserCustomFilters { get; set; } = new List<UserCustomFilter>();

    public virtual ICollection<UserStoryInteractionFilterSetting> UserStoryInteractionFilterSettings { get; set; } = new List<UserStoryInteractionFilterSetting>();
}
