using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NpgsqlTypes;

namespace TheCanalaveLibrary.Models;

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

    [MaxLength(512)]
    public string? CoverArtRelativeUrl { get; set; }
    
    // --- 2. Add the new FTS column ---
    /// <summary>
    /// The auto-generated search vector for Full-Text Search.
    /// This column is populated and indexed by the database.
    /// </summary>
    public NpgsqlTsVector SearchVector { get; set; } = null!;

    // --- Navigation Property ---
    public virtual Story Story { get; set; } = null!;
}