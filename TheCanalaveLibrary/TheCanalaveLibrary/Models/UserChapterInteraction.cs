using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class UserChapterInteraction
{
    public int UserId { get; set; }

    public int ChapterId { get; set; }

    public bool IsRead { get; set; } = false;

    public float ReadProgress { get; set; } = 0f;

    public DateTime LastInteractionDate { get; set; }

    public virtual Chapter Chapter { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
