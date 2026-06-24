using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TheCanalaveLibrary.Core;

/// <summary>The "cold" table — large text blob for one recommendation. 1-to-1 vertical partition.</summary>
public class RecommendationDetail
{
    [Key]
    [ForeignKey("Recommendation")]
    public int RecommendationId { get; set; }

    [Required]
    public string Text { get; set; } = null!;

    public Recommendation Recommendation { get; set; } = null!;
}
