using System.ComponentModel.DataAnnotations;
using TheCanalaveLibrary.Core.Tags;

namespace TheCanalaveLibrary.Core.Models;

public partial class SettingDetail
{
    public int SettingDetailId { get; set; }

    public int StoryId { get; set; }

    public int BaseTagId { get; set; }

    [MaxLength(128)]
    public string? Name { get; set; }

    [MaxLength(2048)]
    public string? Description { get; set; }

    public virtual Tag BaseTag { get; set; } = null!;

    public virtual Story.Story Story { get; set; } = null!;
}
