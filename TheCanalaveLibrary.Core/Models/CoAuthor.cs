namespace TheCanalaveLibrary.Core.Models;

public partial class CoAuthor
{
    public int StoryId { get; set; }

    public int CoAuthorUserId { get; set; }

    public DateTime DateAdded { get; set; }

    public virtual User CoAuthorUser { get; set; } = null!;

    public virtual Story Story { get; set; } = null!;
}
