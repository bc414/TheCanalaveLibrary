using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class SettingDetail
{
    public int SettingDetailId { get; set; }

    public int StoryId { get; set; }

    public int BaseTagId { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public virtual Tag BaseTag { get; set; } = null!;

    public virtual Story Story { get; set; } = null!;
}
