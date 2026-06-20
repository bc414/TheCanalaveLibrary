using System.ComponentModel.DataAnnotations;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Core;

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

    public virtual Story Story { get; set; } = null!;
}
