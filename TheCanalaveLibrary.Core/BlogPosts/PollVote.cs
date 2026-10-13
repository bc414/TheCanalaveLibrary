namespace TheCanalaveLibrary.Core;

/// <summary>
/// One vote: a user's pick of one poll option. A multi-select vote is one row per picked option;
/// single-choice polls (<c>AllowMultiple = false</c>) are limited to one row per voter by the service.
/// <para><b>Keyed by a surrogate <see cref="PollVoteId"/></b> (owner ruling D13, WU-UserDeletion
/// 2026-10-13). <see cref="UserId"/> is nullable: a deleted account's votes survive anonymized
/// (<c>ON DELETE SET NULL</c>), because a poll is a point-in-time record and cascading its votes
/// rewrote the published result. One live vote per (option, user) is enforced by the unique index
/// <c>ix_poll_votes_poll_option_id_user_id</c>, which keeps Postgres's default NULLS DISTINCT so any
/// number of anonymized rows can share an option (<c>layer1-data-model.md</c> §"Column
/// Conventions").</para>
/// </summary>
public class PollVote
{
    public int PollVoteId { get; set; }
    public int PollOptionId { get; set; }

    /// <summary>The voter; <c>null</c> once their account was deleted (an anonymized vote).</summary>
    public int? UserId { get; set; }

    /// <summary>
    /// Only meaningful when the poll's <see cref="PollAnonymityMode"/> is <c>VoterChoice</c>:
    /// true = this voter opted to stay anonymous in the public voter list.
    /// </summary>
    public bool IsAnonymous { get; set; }

    // --- Navigation Properties ---
    public virtual PollOption PollOption { get; set; } = null!;
    public virtual User? User { get; set; }
}
