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
    /// <summary>Sends a single message. Equivalent to a one-element <see cref="SendBatchAsync"/>.</summary>
    Task SendAsync(OutgoingMail mail, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a batch over <b>one</b> connection — the reason this method exists. Notification
    /// fan-out routinely produces tens of messages per drain cycle; a connect/auth/disconnect per
    /// message is the cost the write-behind design exists to avoid
    /// (<c>layer2-services.md</c> §"Email fan-out").
    ///
    /// <para><b>Failure contract, relied on by <see cref="NotificationEmailFlusher"/>:</b>
    /// a <em>connection-level</em> failure (cannot connect, auth rejected) throws, and the caller
    /// restores the whole batch to retry next cycle. A <em>per-message</em> failure (rejected
    /// recipient, malformed address) is logged, counted on <c>canalave.email.failed</c>, and
    /// <b>dropped</b> — it does not throw and is not retried, because a message the server rejects
    /// on its merits will be rejected identically forever.</para>
    /// </summary>
    Task SendBatchAsync(IReadOnlyList<OutgoingMail> batch, CancellationToken cancellationToken = default);
}
