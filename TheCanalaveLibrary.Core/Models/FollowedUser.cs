namespace TheCanalaveLibrary.Core.Models;

public partial class FollowedUser
{
    public int UserId { get; set; }

    public int FollowedUserId { get; set; }

    public DateTime DateFollowed { get; set; }

    public bool ReceiveAlerts { get; set; } = true;

    public bool IsVouched { get; set; } = false;

    public virtual User FollowedUserNavigation { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
