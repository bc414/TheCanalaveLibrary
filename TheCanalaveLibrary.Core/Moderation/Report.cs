using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

public partial class Report
{
    public long ReportId { get; set; }

    public int? ReporterUserId { get; set; }

    public ReportedEntityType ReportedEntityType { get; set; }

    /// <summary>
    /// <c>long</c> to accommodate PrivateMessage ids (which use long keys).
    /// All other reportable entity ids fit in int but the column is wide for uniformity.
    /// </summary>
    public long ReportedEntityId { get; set; }

    public short ReportReasonId { get; set; }

    [MaxLength(2048)]
    public string? Notes { get; set; }

    public ReportStatusEnum ReportStatusId { get; set; }

    public int? ModeratorUserId { get; set; }

    [MaxLength(1024)]
    public string? ActionTaken { get; set; }

    public DateTime DateReported { get; set; }

    public DateTime? DateResolved { get; set; }

    public virtual User? ModeratorUser { get; set; }

    public virtual ReportReason ReportReason { get; set; } = null!;

    public virtual ReportStatus ReportStatus { get; set; } = null!;

    public virtual User? ReporterUser { get; set; }
}
