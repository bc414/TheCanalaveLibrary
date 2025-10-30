using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class StoryAcknowledgment
{
    public int StoryId { get; set; }

    public int AcknowledgedUserId { get; set; }

    public byte AcknowledgmentRoleId { get; set; }

    public DateTime DateAcknowledged { get; set; }

    public virtual User AcknowledgedUser { get; set; } = null!;

    public virtual AcknowledgmentRole AcknowledgmentRole { get; set; } = null!;

    public virtual Story Story { get; set; } = null!;
}
