namespace TheCanalaveLibrary.Server;

/// <summary>
/// One outbound email, fully composed. Deliberately a dumb payload — subject/body composition
/// belongs to the caller (<see cref="EmailBodies"/> for Identity's transactional mail,
/// <see cref="NotificationEmailBodies"/> for notification fan-out), so the exact rendered shape
/// stays unit-testable without a live SMTP connection.
/// </summary>
/// <param name="ToAddress">Recipient address.</param>
/// <param name="Subject">Subject line.</param>
/// <param name="HtmlBody">HTML body. Already encoded — the transport never re-encodes (see
/// <see cref="EmailBodies"/>'s class doc for the double-encoding bug that rule exists to prevent).</param>
/// <param name="Kind">Short telemetry discriminator (e.g. <c>Confirmation</c>,
/// <c>Notification.NewStoryComment</c>). Becomes the <c>canalave.email.kind</c> tag on the span and
/// on the sent/failed counters, so a provider outage shows up as a metric split by what broke.</param>
/// <param name="UserId">Recipient's user id — the correlatable identifier used in logs. The
/// recipient's <em>address</em> is deliberately never logged (<c>logging.md</c> "What NOT to log").</param>
/// <param name="Headers">Extra MIME headers, e.g. <c>List-Unsubscribe</c> /
/// <c>List-Unsubscribe-Post</c> on notification mail. Null for plain transactional mail.</param>
public sealed record OutgoingMail(
    string ToAddress,
    string Subject,
    string HtmlBody,
    string Kind,
    int UserId,
    IReadOnlyDictionary<string, string>? Headers = null);

/// <summary>
/// The single outbound-mail seam for the whole app. Extracted from <see cref="SmtpEmailSender"/>
/// at WU-NotifEmail (2026-07-31) so notification fan-out and Identity's transactional mail share one
/// transport, one <see cref="EmailOptions"/> binding, one provider switch, and one set of
/// <see cref="Core.CanalaveTelemetry.Email"/> signals.
///
/// <para><b>Why notification mail does not ride <c>IEmailSender&lt;User&gt;</c>:</b> that is
/// Identity's fixed three-method contract (confirmation link / password-reset link / password-reset
/// code). It cannot express "send this arbitrary composed message," so a general seam is required.
/// <see cref="SmtpEmailSender"/> is now a thin adapter from Identity's contract onto this one.</para>
///
/// <para><b>Provider independence.</b> Every candidate transactional provider (Postmark, SES,
/// Resend, SendGrid, Mailgun) exposes SMTP, so choosing one is a change to <c>Email:Smtp</c>
/// configuration plus sending-domain DNS — never a code change behind this interface. See
/// <c>roadmap.md</c> decision row 8 and tracker F4.</para>
/// </summary>
public interface IMailTransport
{
    /// <summary>
    /// Sends a single message. Equivalent to a one-element <see cref="SendBatchAsync"/>, except that a
    /// batch abort surfaces as its <em>inner</em> exception, not as
    /// <see cref="MailBatchInterruptedException"/>: Identity's transactional mail
    /// (<see cref="SmtpEmailSender"/>) sees the same exception classes it always saw for a connect
    /// failure. A per-message failure (see <see cref="SendBatchAsync"/>) is logged and dropped, as in a
    /// batch — and so is a deferral, because this path has no queue to retry from.
    /// </summary>
    Task SendAsync(OutgoingMail mail, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a batch over <b>one</b> connection — the reason this method exists. Notification
    /// fan-out routinely produces tens of messages per drain cycle; a connect/auth/disconnect per
    /// message is the cost the write-behind design exists to avoid
    /// (<c>layer2-services.md</c> §"Email fan-out").
    ///
    /// <para><b>Failure contract, relied on by <see cref="NotificationEmailFlusher"/>
    /// (WU-NotificationCorrectness, 2026-10-12; the deferral class added by its review fixes,
    /// 2026-10-13).</b> Besides success, a message has one of three outcomes:</para>
    /// <list type="bullet">
    ///   <item><b>Per-message terminal</b> — a message the same input will fail identically forever: it
    ///   could not be composed (malformed address, bad header), or the server answered it with a 5xx
    ///   while the connection stayed up (rejected recipient). Logged, counted on
    ///   <c>canalave.email.failed</c>, <b>dropped</b>; the batch continues and nothing is thrown.</item>
    ///   <item><b>Deferred</b> — the server answered the message's <c>RCPT TO</c> with a 4xx other than
    ///   421 while the connection stayed up (full mailbox, greylisting, a per-recipient rate limit).
    ///   Not sent; the batch continues, and the message's index is in the returned list so the caller
    ///   can retry it later. Not counted as failed and not logged here — the caller decides when to
    ///   give up on it. A 4xx about one recipient must not stop the batch: the retried mail would lead
    ///   the restored tail and stop every later batch the same way.</item>
    ///   <item><b>Batch abort</b> — everything else: connect/auth failure, a 4xx to the connection,
    ///   the sender or the message data (421 anywhere, provider throttling included), a protocol error,
    ///   a dropped connection, any failure once the client has disconnected, and cancellation between
    ///   messages. The transport throws <see cref="MailBatchInterruptedException"/> whose
    ///   <see cref="MailBatchInterruptedException.ProcessedCount"/> counts the <b>leading</b> messages
    ///   it finished with (and whose <see cref="MailBatchInterruptedException.DeferredIndices"/> lists
    ///   the deferred ones among them); the rest were not sent and the caller restores exactly those.
    ///   A transport that processed one or more messages before failing <b>must</b> wrap, or the caller
    ///   can only choose between dropping the remainder and re-sending the sent head. Aborts are not
    ///   counted as failed (they are retried) and are not logged here — the caller is the one log
    ///   site.</item>
    /// </list>
    /// <para>Accepted edge: when the server accepted DATA but its reply was lost, that one message is
    /// not counted as processed and may be delivered twice (at-least-once for exactly one message).</para>
    /// </summary>
    /// <returns>The indices of the messages the server deferred, ascending; empty when none.</returns>
    Task<IReadOnlyList<int>> SendBatchAsync(IReadOnlyList<OutgoingMail> batch, CancellationToken cancellationToken = default);
}
