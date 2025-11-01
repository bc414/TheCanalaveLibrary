using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Models;

public partial class ReportStatus
{
    public ReportStatusEnum ReportStatusId { get; set; }

    [Required]
    [MaxLength(128)]
    public string StatusName { get; set; } = null!;

    public virtual ICollection<Report> Reports { get; set; } = new List<Report>();
}
