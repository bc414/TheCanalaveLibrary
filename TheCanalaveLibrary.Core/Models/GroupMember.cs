namespace TheCanalaveLibrary.Core.Models;

public partial class GroupMember
{
    public int UserId { get; set; }

    public int GroupId { get; set; }

    public short Role { get; set; }

    public bool NotifyForNewStory { get; set; } = true;
    public bool NotifyForNewBlogPost { get; set; } = false;

    public DateTime DateJoined { get; set; }

    public virtual Group Group { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
