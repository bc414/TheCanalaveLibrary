using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class TagType
{
    public byte TagTypeId { get; set; }

    public string TypeName { get; set; } = null!;

    public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();
}
