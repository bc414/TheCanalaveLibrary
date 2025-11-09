using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core.Models;

public partial class Badge
{
    [Key]
    [Required]
    [MaxLength(128)]
    public string BadgeKey { get; set; } = null!;

    [Required]
    [MaxLength(128)]
    public string DisplayName { get; set; } = null!;

    [MaxLength(2048)]
    public string? Description { get; set; }
    
    [MaxLength(512)]
    public string IconBaseUrl { get; set; } = null!;

    public int SortOrder { get; set; }

    public virtual ICollection<UserBadge> UserBadges { get; set; } = new List<UserBadge>();
}
