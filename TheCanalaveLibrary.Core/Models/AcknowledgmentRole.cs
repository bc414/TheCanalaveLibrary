using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core.Models;

public partial class AcknowledgmentRole
{
    public short AcknowledgmentRoleId { get; set; }

    [Required]
    [MaxLength(256)]
    public string RoleName { get; set; } = null!;

    public virtual ICollection<StoryAcknowledgment> StoryAcknowledgments { get; set; } = new List<StoryAcknowledgment>();
}
