using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class ReportReason
{
    public byte ReportReasonId { get; set; }

    public string ReasonName { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<Report> Reports { get; set; } = new List<Report>();
}
