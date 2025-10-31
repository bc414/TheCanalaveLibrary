using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class ReportStatus
{
    public ReportStatusEnum ReportStatusId { get; set; }

    public string StatusName { get; set; } = null!;

    public virtual ICollection<Report> Reports { get; set; } = new List<Report>();
}
