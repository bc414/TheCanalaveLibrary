using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Models;

/// <summary>
/// A user interaction filter represents a set of stories that a user may want to exclude from search results
/// so that they can discover other stories. Examples are Favorite, Read it Later, Following, Ignored.
/// A user can set different default states for their filters per search mode.
/// </summary>
public partial class UserInteractionFilter
{
    [Key]
    [Required]
    [MaxLength(50)]
    public string InteractionFilterKey { get; set; } = null!;

    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = null!;

    [MaxLength(2048)]
    public string? Description { get; set; }

    public virtual ICollection<DefaultSearchSetting> DefaultSearchSettings { get; set; } = new List<DefaultSearchSetting>();

    public virtual ICollection<UserSearchSetting> UserSearchSettings { get; set; } = new List<UserSearchSetting>();
}
