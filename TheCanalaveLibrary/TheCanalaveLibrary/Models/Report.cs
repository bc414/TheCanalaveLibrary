using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class Report
{
    public long ReportId { get; set; }

    public int? ReporterUserId { get; set; }

    public byte ReportedEntityTypeId { get; set; }

    public int ReportedEntityId { get; set; }

    public byte ReportReasonId { get; set; }

    public string? Notes { get; set; }

    public byte ReportStatusId { get; set; }

    public int? ModeratorUserId { get; set; }

    public string? ActionTaken { get; set; }

    public DateTime DateReported { get; set; }

    public DateTime? DateResolved { get; set; }

    public virtual User? ModeratorUser { get; set; }

    public virtual ReportReason ReportReason { get; set; } = null!;

    public virtual ReportStatus ReportStatus { get; set; } = null!;

    public virtual User? ReporterUser { get; set; }
}
