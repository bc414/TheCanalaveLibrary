namespace TheCanalaveLibrary.Core.Models;

public partial class SeriesEntry
{
    public int SeriesId { get; set; }

    public int StoryId { get; set; }

    public int OrderIndex { get; set; }

    public virtual Series Series { get; set; } = null!;

    public virtual Story Story { get; set; } = null!;
}
