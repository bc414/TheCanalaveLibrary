using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// A SearchMode represents a way to search for stories on the site. Examples are RandomSearch, TreeSearch.
/// Each SearchMode can have a different set of default filter criteria and user overrides depenending on
/// how the mode is designed.
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

    public virtual ICollection<DefaultSearchSetting> DefaultSearchSettings { get; set; } = new List<DefaultSearchSetting>();

    public virtual ICollection<UserCustomFilter> UserCustomFilters { get; set; } = new List<UserCustomFilter>();

    public virtual ICollection<UserSearchSetting> UserSearchSettings { get; set; } = new List<UserSearchSetting>();
}
