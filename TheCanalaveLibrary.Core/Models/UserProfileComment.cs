namespace TheCanalaveLibrary.Core.Models;

public partial class UserProfileComment : BaseComment
{
    public int ProfileUserId { get; set; }

    public virtual User ProfileUser { get; set; } = null!;
}
