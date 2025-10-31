using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class StoryStatus
{
    public StoryStatusEnum StoryStatusId { get; set; }

    public string StatusName { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<Story> Stories { get; set; } = new List<Story>();
}
