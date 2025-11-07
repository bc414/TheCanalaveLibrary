using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Models;

public partial class ReportReason
{
    public short ReportReasonId { get; set; }

    [Required]
    [MaxLength(128)]
    public string ReasonName { get; set; } = null!;

    [MaxLength(2048)]
    public string? Description { get; set; }

    public virtual ICollection<Report> Reports { get; set; } = new List<Report>();
}
