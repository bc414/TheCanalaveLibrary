using System.Diagnostics;
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
    public Task SendAsync(OutgoingMail mail, CancellationToken cancellationToken = default) =>
        SendBatchAsync([mail], cancellationToken);

    /// <inheritdoc/>
    public async Task SendBatchAsync(IReadOnlyList<OutgoingMail> batch, CancellationToken cancellationToken = default)
    {
        if (batch.Count == 0) return;

        EmailOptions o = options.Value;

        using var client = new SmtpClient();
        SecureSocketOptions socketOptions = o.Smtp.UseStartTls
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.None;

        // Connection-level failures propagate: the caller (NotificationEmailFlusher) restores the
        // whole batch and retries next cycle. Nothing was sent, so nothing is duplicated.
        await client.ConnectAsync(o.Smtp.Host, o.Smtp.Port, socketOptions, cancellationToken);
        try
        {
            if (!string.IsNullOrEmpty(o.Smtp.User))
                await client.AuthenticateAsync(o.Smtp.User, o.Smtp.Password ?? "", cancellationToken);

            foreach (OutgoingMail mail in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await SendOneAsync(client, o, mail, cancellationToken);
            }
        }
        finally
        {
            // Best-effort quit — a failure closing an already-doomed connection must not mask the
            // real exception on its way out (logging.md: no silent catches — this one is logged).
            try
            {
                await client.DisconnectAsync(quit: true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "SMTP disconnect failed after sending {BatchSize} message(s).", batch.Count);
            }
        }
    }

    private async Task SendOneAsync(SmtpClient client, EmailOptions o, OutgoingMail mail, CancellationToken cancellationToken)
    {
        // Custom span: HttpClient/socket instrumentation is blind to SMTP — nothing names "one
        // email" as a unit (logging.md §"Custom Instrumentation").
        using Activity? activity = CanalaveTelemetry.Email.Source.StartActivity("Email.Send");
        activity?.SetTag("canalave.email.kind", mail.Kind);

        try
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

            await client.SendAsync(message, cancellationToken);

            CanalaveTelemetry.Email.Sent.Add(1, new KeyValuePair<string, object?>("canalave.email.kind", mail.Kind));
            // Recipient address deliberately excluded from the log — logging.md "What NOT to
            // log": email addresses. The user id is the correlatable identifier instead.
            logger.LogInformation("Sent {EmailKind} email to user {UserId}", mail.Kind, mail.UserId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            CanalaveTelemetry.Email.Failed.Add(1, new KeyValuePair<string, object?>("canalave.email.kind", mail.Kind));
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            // Per-message failure is terminal for THIS message, not for the batch — see
            // IMailTransport.SendBatchAsync's failure contract. A message the server rejects on its
            // merits (bad address, refused recipient) fails identically on every retry, so retrying
            // it forever would wedge the queue behind one bad row. Logged here rather than rethrown,
            // which makes this the one place the no-double-log rule resolves in favour of logging.
            logger.LogError(ex, "Failed to send {EmailKind} email to user {UserId}; message dropped.",
                mail.Kind, mail.UserId);
        }
    }
}
