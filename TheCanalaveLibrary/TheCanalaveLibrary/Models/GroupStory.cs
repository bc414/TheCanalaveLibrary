using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class GroupStory
{
    public int GroupId { get; set; }

    public int StoryId { get; set; }

    public int? AddedByUserId { get; set; }

    public DateTime DateAdded { get; set; }

    public virtual User? AddedByUser { get; set; }

    public virtual Group Group { get; set; } = null!;

    public virtual Story Story { get; set; } = null!;
}
