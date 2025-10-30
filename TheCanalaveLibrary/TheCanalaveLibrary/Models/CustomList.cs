using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class CustomList
{
    public int ListId { get; set; }

    public int UserId { get; set; }

    public string ListName { get; set; } = null!;

    public bool IsPublic { get; set; }

    public DateTime DateCreated { get; set; }

    public virtual ICollection<CustomListEntry> CustomListEntries { get; set; } = new List<CustomListEntry>();

    public virtual User User { get; set; } = null!;
}
