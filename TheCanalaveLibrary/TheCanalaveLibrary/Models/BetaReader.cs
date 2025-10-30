using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class BetaReader
{
    public int StoryId { get; set; }

    public int BetaReaderUserId { get; set; }

    public DateTime DateAdded { get; set; }

    public virtual User BetaReaderUser { get; set; } = null!;

    public virtual Story Story { get; set; } = null!;
}
