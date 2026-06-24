using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

public class RecommendationStatus
{
    public short RecommendationStatusId { get; set; }

    [Required]
    [MaxLength(50)]
    public string StatusName { get; set; } = null!;

    [Required]
    [MaxLength(255)]
    public string Description { get; set; } = null!;

    public ICollection<Recommendation> Recommendations { get; set; } = [];
}
