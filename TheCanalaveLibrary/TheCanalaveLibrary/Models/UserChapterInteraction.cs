using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class UserChapterInteraction
{
    public int UserId { get; set; }

    public int ChapterId { get; set; }

    public double ReadProgress { get; set; }

    public DateTime LastInteractionDate { get; set; }

    public virtual Chapter Chapter { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
