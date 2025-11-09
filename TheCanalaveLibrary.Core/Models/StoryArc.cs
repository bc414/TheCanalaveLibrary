using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core.Models;

public partial class StoryArc
{
    public int StoryArcId { get; set; }

    public int StoryId { get; set; }

    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = null!;

    public int SortOrder { get; set; }

    public int StartChapterNumber { get; set; }

    public int EndChapterNumber { get; set; }

    public virtual Story Story { get; set; } = null!;
}
