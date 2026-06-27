namespace TheCanalaveLibrary.Core;

/// <summary>
/// Marker interface for the four content roots that share the soft-delete / takedown column shape:
/// <see cref="Story"/>, <see cref="BaseComment"/>, <see cref="BaseBlogPost"/>,
/// <see cref="Recommendation"/>. The interface allows <c>ServerModerationWriteService</c> to
/// mutate all four through a single shared code path rather than repeating a switch statement for
/// each operation.
///
/// <para><b>Does not cover:</b> <c>User</c> (counter only; account actions are a separate path)
/// or <c>PrivateMessage</c> (neither counter nor takedown — hard-delete only per WU34). Those stay
/// explicitly special-cased in the write service.</para>
/// </summary>
public interface IModeratableContent
{
    bool IsTakenDown { get; set; }
    DateTime? TakedownDate { get; set; }
    string? TakedownReason { get; set; }
    int ActiveReportCount { get; set; }

    /// <summary>
    /// Read-only projection of the root's author/owner FK — used after load to notify the author.
    /// Returns <c>null</c> if the content was posted anonymously or the author was deleted.
    /// </summary>
    int? AuthorUserId { get; }
}
