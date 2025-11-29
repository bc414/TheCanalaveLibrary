using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core.Models;

public partial class StoryStatus
{
    public StoryStatusEnum StoryStatusId { get; set; }

    [Required]
    [MaxLength(20)]
    public string StatusName { get; set; } = null!;

    [Required] [MaxLength(255)] public string Description { get; set; } = null!;

    public virtual ICollection<Story.Story> Stories { get; set; } = new List<Story.Story>();
}
