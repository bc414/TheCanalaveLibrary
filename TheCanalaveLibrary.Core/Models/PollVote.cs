namespace TheCanalaveLibrary.Core;

/// <summary>
/// Explicit junction for a user's vote on a poll option. One row per (option, user).
/// </summary>
public class PollVote
{
    public int PollOptionId { get; set; }
    public int UserId { get; set; }

    // --- Navigation Properties ---
    public virtual PollOption PollOption { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
