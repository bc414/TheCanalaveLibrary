using System.Diagnostics;
using System.Runtime.ExceptionServices;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// MailKit/SMTP implementation of <see cref="IMailTransport"/> — the app's only real outbound-mail
/// path. Sends over whatever <see cref="EmailOptions.Smtp"/> points at: the Mailpit dev inbox under
/// the Aspire path, or the chosen provider's SMTP endpoint in production (decision row 8,
/// `.claude/roadmap.md`). Selected by the <c>Email:Provider = "Smtp"</c> switch in Program.cs;
/// <see cref="NoOpMailTransport"/> stays registered when unconfigured.
///
/// <para>This is the send body extracted verbatim from <c>SmtpEmailSender</c> at WU-NotifEmail
/// (2026-07-31), plus <see cref="SendBatchAsync"/>'s single-connection loop. Both the Identity
/// transactional path and notification fan-out now run through here, so the
/// <see cref="CanalaveTelemetry.Email"/> span and sent/failed counters cover all outbound mail
/// rather than only Identity's three messages.</para>
///
/// <para><b>Failure classification (WU-NotificationCorrectness, 2026-10-12).</b> Before it, every
/// non-cancellation send exception was swallowed as "per-message", connection-class ones included,
/// so after a mid-batch disconnect each remaining message failed <c>ServiceNotConnected</c> and was
/// dropped forever. Now a send failure is per-message terminal only when
/// <see cref="IsPerMessageFailure"/> says so; anything else stops the batch with
/// <see cref="MailBatchInterruptedException"/> carrying how many leading messages were processed
/// (<see cref="IMailTransport.SendBatchAsync"/>'s contract).</para>
///
/// <para><b>Singleton, and safe as one:</b> holds no connection state between calls — every
/// <see cref="SendBatchAsync"/> constructs, uses, and disposes its own <see cref="SmtpClient"/>.
/// MailKit's client is explicitly not thread-safe, so it must never be hoisted to a field.</para>
///
/// <para><c>public</c> (not <c>internal</c>) per the project's test-seam convention (see
/// <c>ServerWriteRateLimitService</c>) — the repo deliberately carries no <c>InternalsVisibleTo</c>,
/// so anything Unit-tested is public.</para>
/// </summary>
public sealed class SmtpMailTransport(IOptions<EmailOptions> options, ILogger<SmtpMailTransport> logger)
    : IMailTransport
{
    /// <summary>
    /// The single-message path (Identity's confirmation/reset mail). A batch abort is unwrapped and
    /// its inner exception rethrown, so Identity sees the exception classes it always saw for a
    /// connect failure rather than the batch wrapper.
    /// </summary>
    public async Task SendAsync(OutgoingMail mail, CancellationToken cancellationToken = default)
    {
        try
        {
            await SendBatchAsync([mail], cancellationToken);
        }
        catch (MailBatchInterruptedException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }
    }

    /// <inheritdoc/>
    public async Task SendBatchAsync(IReadOnlyList<OutgoingMail> batch, CancellationToken cancellationToken = default)
    {
        if (batch.Count == 0) return;

        EmailOptions o = options.Value;

        using var client = new SmtpClient();
        SecureSocketOptions socketOptions = o.Smtp.UseStartTls
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.None;

        // Leading messages that reached a terminal outcome (sent, or dropped per message). Anything
        // that stops the loop reports it, so the caller restores exactly the unsent tail: the
        // remainder is not lost and the sent head is not duplicated.
        int processed = 0;
        try
        {
            // A connect/auth failure stops the batch with processed = 0: nothing was sent.
            await client.ConnectAsync(o.Smtp.Host, o.Smtp.Port, socketOptions, cancellationToken);
            if (!string.IsNullOrEmpty(o.Smtp.User))
                await client.AuthenticateAsync(o.Smtp.User, o.Smtp.Password ?? "", cancellationToken);

            foreach (OutgoingMail mail in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await SendOneAsync(client, o, mail, cancellationToken);
                processed++;
            }
        }
        catch (Exception ex)
        {
            // Not logged here: the unsent tail is retried, and the caller (NotificationEmailFlusher,
            // or Identity through SendAsync) is the one log site (logging.md no-double-log).
            throw new MailBatchInterruptedException(processed, ex);
        }
        finally
        {
            // Best-effort quit — a failure closing an already-doomed connection must not mask the
            // real exception on its way out (logging.md: no silent catches — this one is logged).
            if (client.IsConnected)
            {
                try
                {
                    await client.DisconnectAsync(quit: true, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex,
                        "SMTP disconnect failed after processing {ProcessedCount} of {BatchSize} message(s).",
                        processed, batch.Count);
                }
            }
        }
    }

    /// <summary>
    /// The per-message vs. batch-abort rule for an exception thrown by <c>SmtpClient.SendAsync</c>:
    /// per-message terminal <b>only</b> for an <see cref="SmtpCommandException"/> with a 5xx status
    /// while the client is still connected — the server judged this message on its merits and will
    /// judge it the same way on every retry. Everything else aborts the batch: a 4xx is transient
    /// (provider throttling such as 421/451/454 included), and a protocol error, a dropped
    /// connection (<c>ServiceNotConnectedException</c>, <see cref="IOException"/>,
    /// <c>SocketException</c>) or any failure after the client disconnected would fail every
    /// remaining message for reasons that have nothing to do with them.
    /// </summary>
    /// <param name="ex">The exception <c>SendAsync</c> threw.</param>
    /// <param name="clientStillConnected"><c>SmtpClient.IsConnected</c>, read after the throw.</param>
    public static bool IsPerMessageFailure(Exception ex, bool clientStillConnected) =>
        clientStillConnected
        && ex is SmtpCommandException command
        && (int)command.StatusCode is >= 500 and <= 599;

    /// <summary>
    /// Composes and sends one message. Returns normally when the message reached a terminal outcome
    /// (sent, or dropped as a per-message failure); throws when the batch must stop.
    /// </summary>
    private async Task SendOneAsync(SmtpClient client, EmailOptions o, OutgoingMail mail, CancellationToken cancellationToken)
    {
        // Custom span: HttpClient/socket instrumentation is blind to SMTP — nothing names "one
        // email" as a unit (logging.md §"Custom Instrumentation").
        using Activity? activity = CanalaveTelemetry.Email.Source.StartActivity("Email.Send");
        activity?.SetTag("canalave.email.kind", mail.Kind);

        MimeMessage message;
        try
        {
            message = Compose(o, mail);
        }
        catch (Exception ex)
        {
            // Composition is deterministic (MailboxAddress.Parse's ParseException, a header
            // ArgumentException): the same input fails the same way forever, so it is per-message
            // terminal and never stops the batch.
            RecordDropped(activity, ex, mail);
            return;
        }

        try
        {
            await client.SendAsync(message, cancellationToken);
        }
        catch (Exception ex) when (IsPerMessageFailure(ex, client.IsConnected))
        {
            RecordDropped(activity, ex, mail);
            return;
        }
        catch (Exception ex)
        {
            // Batch abort: the span records it, the batch wrapper carries it to the caller, and it is
            // NOT counted as failed — the message is retried, not lost.
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }

        CanalaveTelemetry.Email.Sent.Add(1, new KeyValuePair<string, object?>("canalave.email.kind", mail.Kind));
        // Recipient address deliberately excluded from the log — logging.md "What NOT to
        // log": email addresses. The user id is the correlatable identifier instead.
        logger.LogInformation("Sent {EmailKind} email to user {UserId}", mail.Kind, mail.UserId);
    }

    private static MimeMessage Compose(EmailOptions o, OutgoingMail mail)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(o.FromName, o.FromAddress));
        message.To.Add(MailboxAddress.Parse(mail.ToAddress));
        message.Subject = mail.Subject;
        message.Body = new TextPart("html") { Text = mail.HtmlBody };

        if (mail.Headers is not null)
        {
            foreach ((string name, string value) in mail.Headers)
                message.Headers.Add(name, value);
        }

        return message;
    }

    /// <summary>
    /// A per-message terminal failure: counted, recorded on the span, logged, and dropped — retrying a
    /// message the server (or the composer) rejects on its merits would wedge the queue behind one bad
    /// row. Logged here rather than rethrown, which makes this the one place the no-double-log rule
    /// resolves in favour of logging.
    /// </summary>
    private void RecordDropped(Activity? activity, Exception ex, OutgoingMail mail)
    {
        CanalaveTelemetry.Email.Failed.Add(1, new KeyValuePair<string, object?>("canalave.email.kind", mail.Kind));
        activity?.AddException(ex);
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        logger.LogError(ex, "Failed to send {EmailKind} email to user {UserId}; message dropped.",
            mail.Kind, mail.UserId);
    }
}
