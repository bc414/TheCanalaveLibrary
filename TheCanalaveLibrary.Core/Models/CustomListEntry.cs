namespace TheCanalaveLibrary.Core.Models;

public partial class CustomListEntry
{
    public int ListId { get; set; }

    public int StoryId { get; set; }

    public DateTime DateAdded { get; set; }

    public virtual CustomList List { get; set; } = null!;

    public virtual Story.Story Story { get; set; } = null!;
}
