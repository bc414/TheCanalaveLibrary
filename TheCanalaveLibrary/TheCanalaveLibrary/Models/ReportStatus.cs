using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class ReportStatus
{
    public byte ReportStatusId { get; set; }

    public string StatusName { get; set; } = null!;

    public virtual ICollection<Report> Reports { get; set; } = new List<Report>();
}
