using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class Series
{
    public int SeriesId { get; set; }

    public int? AuthorId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime DateCreated { get; set; }

    public virtual User? Author { get; set; }

    public virtual ICollection<SeriesEntry> SeriesEntries { get; set; } = new List<SeriesEntry>();
}
