using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class GroupComment : BaseComment
{
    public int GroupId { get; set; }

    public virtual Group Group { get; set; } = null!;
    
    public DateTime DatePosted { get; set; }
}
