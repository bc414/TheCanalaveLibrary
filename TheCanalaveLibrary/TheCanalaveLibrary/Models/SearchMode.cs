using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class SearchMode
{
    public string SearchModeKey { get; set; } = null!;

    public string Name { get; set; } = null!;

    public virtual ICollection<DefaultSearchSetting> DefaultSearchSettings { get; set; } = new List<DefaultSearchSetting>();

    public virtual ICollection<UserCustomFilter> UserCustomFilters { get; set; } = new List<UserCustomFilter>();

    public virtual ICollection<UserSearchSetting> UserSearchSettings { get; set; } = new List<UserSearchSetting>();
}
