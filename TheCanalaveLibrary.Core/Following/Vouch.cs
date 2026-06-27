namespace TheCanalaveLibrary.Core;

/// <summary>
/// A scarce personal endorsement of one user by another (§5.8, §8.13). Promoted from a boolean on
/// <see cref="FollowedUser"/> to its own table so it can carry optional <see cref="VouchText"/>.
/// The 5-per-user limit is enforced in the C# service layer, not the database. Display asymmetry
/// (outgoing vouches public, incoming private to the owner) is applied at query time.
///
/// <c>VouchText</c> is first-class authored rich HTML (settled WU21): authored in EditorView,
/// displayed via RichTextView, sanitized once on save. The column is unbounded text — the original
/// MaxLength(1000) was removed in the MakeVouchTextUnlimited migration. No WordCount column.
/// </summary>
public class Vouch
{
    public int VouchingUserId { get; set; }
    public int VouchedUserId { get; set; }

    public string? VouchText { get; set; }

    public DateTime DateVouched { get; set; }

    // --- Navigation Properties ---
    public virtual User VouchingUser { get; set; } = null!;
    public virtual User VouchedUser { get; set; } = null!;
}
