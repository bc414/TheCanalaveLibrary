namespace TheCanalaveLibrary.Core;

public partial class GroupMember
{
    public int UserId { get; set; }

    public int GroupId { get; set; }

    /// <summary>
    /// Role within this group. Stored as <c>short</c> via HasConversion (GroupMemberConfiguration).
    /// Two roles only: Member (0) and Admin (1) — settled WU32.
    /// </summary>
    public GroupRole Role { get; set; }

    public bool NotifyForNewStory { get; set; } = true;
    public bool NotifyForNewBlogPost { get; set; } = false;

    public DateTime DateJoined { get; set; }

    public virtual Group Group { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
