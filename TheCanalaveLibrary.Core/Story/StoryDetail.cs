using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TheCanalaveLibrary.Core.Models;

namespace TheCanalaveLibrary.Core.Story;

/// <summary>
/// The "cold" table. Contains large or rarely-accessed data
/// related to a Story. This is a 1-to-1 vertical partition.
/// </summary>
public partial class StoryDetail
{
    [Key]
    [ForeignKey("Story")]
    public int StoryId { get; set; }

    // --- "Cold" Properties ---

    public string? LongDescription { get; set; }

    [MaxLength(255)]
    public string? Slug { get; set; }

    public StoryStatusEnum PostApprovalStatus { get; set; }

    // --- Navigation Properties ---
    public virtual Core.Story.Story Story { get; set; } = null!;
}