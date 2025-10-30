using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class DailyStoryStat
{
    public int StoryId { get; set; }

    public DateOnly StatDate { get; set; }

    public int Views { get; set; }

    public int Favorites { get; set; }

    public virtual Story Story { get; set; } = null!;
}
