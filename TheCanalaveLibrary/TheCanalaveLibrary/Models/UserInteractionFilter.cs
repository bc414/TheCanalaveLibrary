using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class UserInteractionFilter
{
    public string InteractionFilterKey { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<DefaultSearchSetting> DefaultSearchSettings { get; set; } = new List<DefaultSearchSetting>();

    public virtual ICollection<UserSearchSetting> UserSearchSettings { get; set; } = new List<UserSearchSetting>();
}
