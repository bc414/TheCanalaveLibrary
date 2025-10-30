using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class AcknowledgmentRole
{
    public byte AcknowledgmentRoleId { get; set; }

    public string RoleName { get; set; } = null!;

    public virtual ICollection<StoryAcknowledgment> StoryAcknowledgments { get; set; } = new List<StoryAcknowledgment>();
}
