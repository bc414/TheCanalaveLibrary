using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class UserCustomFilter
{
    public int UserCustomFilterId { get; set; }

    public int UserId { get; set; }

    public string SearchModeKey { get; set; } = null!;

    public FilterEntityType FilterEntityType { get; set; }

    public int EntityId { get; set; }

    public bool Include { get; set; }

    public virtual SearchMode SearchModeKeyNavigation { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
