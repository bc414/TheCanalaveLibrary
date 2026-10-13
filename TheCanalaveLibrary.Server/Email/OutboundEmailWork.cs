namespace TheCanalaveLibrary.Server;

/// <summary>
/// Which producer an <see cref="OutboundEmailWork"/> item came from, and so which
/// <see cref="IOutboundEmailComposer"/> turns its id into mail (owner ruling D18,
/// WU-PrivateMessageEmail 2026-10-13). The pipeline itself knows nothing about either domain: a new
/// kind is a new member here plus a composer registration, never a change to the buffer or flusher.
/// </summary>
public enum OutboundEmailKind : byte
{
    /// <summary>A <c>Notification</c> row id, enqueued by notification create-core.</summary>
    Notification = 1,

    /// <summary>A <c>PrivateMessage</c> id, enqueued by Messaging after a send.</summary>
    PrivateMessage = 2,
}

/// <summary>
/// One queued item: a producer's row id, tagged with its kind. Ids only, deliberately — eligibility
/// is resolved against live rows when the batch drains (see <see cref="OutboundEmailBuffer"/>).
/// </summary>
public readonly record struct OutboundEmailWork(OutboundEmailKind Kind, long Id);

/// <summary>
/// Turns one kind's drained ids into mail (<c>layer2-services.md</c> §"Outbound email pipeline").
/// Scoped; <see cref="OutboundEmailFlusher"/> resolves every registered composer per flush and calls
/// each one at most once per batch, with that kind's ids de-duplicated.
///
/// <para>The composer owns its domain's whole say: who is eligible (checked now, at drain time, never
/// at enqueue), what the mail says, its unsubscribe header. Ids it judges ineligible, or that no
/// longer name a row, are simply absent from the result. It must not send anything itself.</para>
/// </summary>
public interface IOutboundEmailComposer
{
    OutboundEmailKind Kind { get; }

    Task<ComposedEmails> ComposeAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken);
}

/// <summary>
/// One composed mail and its <b>restore key</b>: the item that, composed again, would mail this
/// recipient again. The flusher puts the key back on the buffer when the mail was not sent, and counts
/// deferrals against it. For a notification it is the notification's own id; for a private message
/// it is the newest enqueued message id of the conversation.
/// </summary>
public sealed record ComposedEmail(long RestoreId, OutgoingMail Mail);

/// <summary>
/// A composer's output for one batch.
/// </summary>
/// <param name="Mails">The mails to send, in any order.</param>
/// <param name="AfterSendAsync">
/// Optional. Called once per flush that had any of these mails taken by the transport — sent, or
/// dropped as a per-message failure, never deferred or unsent — with the indices into
/// <paramref name="Mails"/> of exactly those mails. Runs after a successful batch and after an aborted
/// one (for its processed head), always with <see cref="CancellationToken.None"/>: the mail has left,
/// and recording that must not be lost to a shutdown. A failure here is logged and never restores
/// anything, because the mail cannot be unsent.
/// </param>
public sealed record ComposedEmails(
    IReadOnlyList<ComposedEmail> Mails,
    Func<IReadOnlyList<int>, CancellationToken, Task>? AfterSendAsync = null)
{
    /// <summary>Nothing to send.</summary>
    public static ComposedEmails None { get; } = new([]);
}
