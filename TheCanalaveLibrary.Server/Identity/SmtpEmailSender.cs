using System.Diagnostics;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MimeKit;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// SMTP implementation of <see cref="IEmailSender{TUser}"/> for <see cref="User"/> — sends the
/// three Identity transactional emails (confirmation, password reset link, password reset code)
/// over MailKit against whatever <see cref="EmailOptions.Smtp"/> points at: the Mailpit dev inbox
/// under the Aspire path, or the chosen provider's SMTP endpoint in production (decision row 8,
/// `.claude/middle_plan_v2.md`). Selected by the <c>Email:Provider = "Smtp"</c> switch in
/// Program.cs; <see cref="IdentityNoOpEmailSender"/> stays registered when unconfigured.
///
/// <c>public</c> (not <c>internal</c>, unlike <see cref="IdentityNoOpEmailSender"/>) per the
/// project's test-seam convention (see <c>ServerWriteRateLimitService</c>) — the repo
/// deliberately carries no <c>InternalsVisibleTo</c>, so anything Unit-tested is public.
/// </summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    : IEmailSender<User>
{
    public Task SendConfirmationLinkAsync(User user, string email, string confirmationLink) =>
        SendAsync(user.Id, email, "Confirmation",
            EmailBodies.ConfirmationSubject, EmailBodies.ConfirmationBody(confirmationLink));

    public Task SendPasswordResetLinkAsync(User user, string email, string resetLink) =>
        SendAsync(user.Id, email, "PasswordResetLink",
            EmailBodies.PasswordResetLinkSubject, EmailBodies.PasswordResetLinkBody(resetLink));

    public Task SendPasswordResetCodeAsync(User user, string email, string resetCode) =>
        SendAsync(user.Id, email, "PasswordResetCode",
            EmailBodies.PasswordResetCodeSubject, EmailBodies.PasswordResetCodeBody(resetCode));

    private async Task SendAsync(int userId, string toAddress, string kind, string subject, string htmlBody)
    {
        EmailOptions o = options.Value;

        // Custom span: HttpClient/socket instrumentation is blind to SMTP — nothing names "one
        // transactional email" as a unit (logging.md §"Custom Instrumentation").
        using Activity? activity = CanalaveTelemetry.Email.Source.StartActivity("Email.Send");
        activity?.SetTag("canalave.email.kind", kind);

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(o.FromName, o.FromAddress));
            message.To.Add(MailboxAddress.Parse(toAddress));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlBody };

            using var client = new SmtpClient();
            SecureSocketOptions socketOptions = o.Smtp.UseStartTls
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;
            await client.ConnectAsync(o.Smtp.Host, o.Smtp.Port, socketOptions);
            if (!string.IsNullOrEmpty(o.Smtp.User))
                await client.AuthenticateAsync(o.Smtp.User, o.Smtp.Password ?? "");
            await client.SendAsync(message);
            await client.DisconnectAsync(quit: true);

            CanalaveTelemetry.Email.Sent.Add(1, new KeyValuePair<string, object?>("canalave.email.kind", kind));
            // Recipient address deliberately excluded from the log — logging.md "What NOT to
            // log": email addresses. The user id is the correlatable identifier instead.
            logger.LogInformation("Sent {EmailKind} email to user {UserId}", kind, userId);
        }
        catch (Exception ex)
        {
            CanalaveTelemetry.Email.Failed.Add(1, new KeyValuePair<string, object?>("canalave.email.kind", kind));
            // Recorded on the span; not logged here — the exception propagates and is logged
            // (with this scope's trace context) wherever it surfaces (logging.md: no double-log).
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
