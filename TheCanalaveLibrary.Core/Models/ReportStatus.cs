using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core.Models;

public partial class ReportStatus
{
    public ReportStatusEnum ReportStatusId { get; set; }

    [Required]
    [MaxLength(128)]
    public string StatusName { get; set; } = null!;

    public virtual ICollection<Report> Reports { get; set; } = new List<Report>();
}
