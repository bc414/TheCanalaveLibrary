using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TheCanalaveLibrary.Core.Story;

/// <summary>
/// The "warm" table. Contains projection data needed for displaying
/// stories in lists (search results, author pages, etc.).
/// This is a 1-to-1 vertical partition from Story.
/// </summary>
public class StoryListing
{
    [Key]
    [ForeignKey("Story")]
    public int StoryId { get; set; }

    // --- "Warm" Properties (for Projections) ---

    [Required]
    [MaxLength(255)]
    public string StoryTitle { get; set; } = null!;

    [MaxLength(500)]
    public string? ShortDescription { get; set; }

    [MaxLength(512)] public string? CoverArtRelativeUrl { get; set; }

    // --- Navigation Property ---
    public virtual Core.Story.Story Story { get; set; } = null!;
}