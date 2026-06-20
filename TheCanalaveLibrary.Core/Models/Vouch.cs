using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// A scarce personal endorsement of one user by another (§5.8, §8.13). Promoted from a boolean on
/// <see cref="FollowedUser"/> to its own table so it can carry optional <see cref="VouchText"/>.
/// The 5-per-user limit is enforced in the C# service layer, not the database. Display asymmetry
/// (outgoing vouches public, incoming private to the owner) is applied at query time.
/// </summary>
public class Vouch
{
    public int VouchingUserId { get; set; }
    public int VouchedUserId { get; set; }

    [MaxLength(1000)]
    public string? VouchText { get; set; }

    public DateTime DateVouched { get; set; }

    // --- Navigation Properties ---
    public virtual User VouchingUser { get; set; } = null!;
    public virtual User VouchedUser { get; set; } = null!;
}
