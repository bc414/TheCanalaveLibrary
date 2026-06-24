namespace TheCanalaveLibrary.Core;

public partial class UserProfileComment : BaseComment
{
    public DateTime DatePosted { get; set; }

    public int ProfileUserId { get; set; }

    public virtual User ProfileUser { get; set; } = null!;
}
