namespace TheCanalaveLibrary.Core;

public partial class FollowedUser
{
    public int UserId { get; set; }

    public int FollowedUserId { get; set; }

    public DateTime DateFollowed { get; set; }

    public bool ReceiveAlerts { get; set; } = true;

    // NOTE: vouching was promoted out of this table into its own Vouch entity (with optional VouchText, §8.13).

    public virtual User FollowedUserNavigation { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
