using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Models;

public partial class DefaultSearchSetting
{
    [Required]
    [MaxLength(50)]
    public string SearchModeKey { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string InteractionFilterKey { get; set; } = null!;

    public bool IsEnabled { get; set; }

    /* Don't remember why we need this
    public string? DefaultValue { get; set; }
    */

    public virtual UserInteractionFilter InteractionFilterKeyNavigation { get; set; } = null!;

    public virtual SearchMode SearchModeKeyNavigation { get; set; } = null!;
}
