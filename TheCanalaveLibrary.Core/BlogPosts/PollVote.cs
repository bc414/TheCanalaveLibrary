namespace TheCanalaveLibrary.Core;

/// <summary>
/// Explicit junction for a user's vote on a poll option. One row per (option, user).
/// Single-choice polls (<c>AllowMultiple = false</c>) are service-enforced — the PK deliberately
/// permits multi-select rows for polls that allow it.
/// </summary>
public class PollVote
{
    public int PollOptionId { get; set; }
    public int UserId { get; set; }

    /// <summary>
    /// Only meaningful when the poll's <see cref="PollAnonymityMode"/> is <c>VoterChoice</c>:
    /// true = this voter opted to stay anonymous in the public voter list.
    /// </summary>
    public bool IsAnonymous { get; set; }

    // --- Navigation Properties ---
    public virtual PollOption PollOption { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
