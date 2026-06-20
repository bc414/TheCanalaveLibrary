using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// The "cold" table. Contains the large text blob for a
/// single recommendation. This is a 1-to-1 vertical partition.
/// </summary>
public partial class RecommendationDetail
{
    [Key]
    [ForeignKey("Recommendation")]
    public int RecommendationId { get; set; }

    // --- "Cold" Property ---
    [Required]
    public string Text { get; set; } = null!;

    // --- Navigation Property ---
    public virtual Recommendation Recommendation { get; set; } = null!;
}