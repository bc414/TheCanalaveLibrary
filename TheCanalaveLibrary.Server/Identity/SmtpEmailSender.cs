using Microsoft.AspNetCore.Identity;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Adapts Identity's <see cref="IEmailSender{TUser}"/> contract for <see cref="User"/> onto the
/// app's general <see cref="IMailTransport"/> — sends the three Identity transactional emails
/// (confirmation, password reset link, password reset code). Selected by the
/// <c>Email:Provider = "Smtp"</c> switch in Program.cs; <see cref="IdentityNoOpEmailSender"/> stays
/// registered when unconfigured.
///
/// <para><b>Reduced to an adapter at WU-NotifEmail (2026-07-31).</b> The MailKit/MimeKit send body,
/// the <c>Email.Send</c> span, and the sent/failed counters that used to live here moved to
/// <see cref="SmtpMailTransport"/> so notification fan-out could share them. Identity's behavior is
/// unchanged — same three messages, same <see cref="EmailBodies"/> composition, same telemetry
/// signals with the same <c>kind</c> tags.</para>
///
/// <para><c>public</c> (not <c>internal</c>, unlike <see cref="IdentityNoOpEmailSender"/>) per the
/// project's test-seam convention (see <c>ServerWriteRateLimitService</c>) — the repo deliberately
/// carries no <c>InternalsVisibleTo</c>, so anything Unit-tested is public.</para>
/// </summary>
public sealed class SmtpEmailSender(IMailTransport transport) : IEmailSender<User>
{
    public Task SendConfirmationLinkAsync(User user, string email, string confirmationLink) =>
        transport.SendAsync(new OutgoingMail(email, EmailBodies.ConfirmationSubject,
            EmailBodies.ConfirmationBody(confirmationLink), "Confirmation", user.Id));

    public Task SendPasswordResetLinkAsync(User user, string email, string resetLink) =>
        transport.SendAsync(new OutgoingMail(email, EmailBodies.PasswordResetLinkSubject,
            EmailBodies.PasswordResetLinkBody(resetLink), "PasswordResetLink", user.Id));

    public Task SendPasswordResetCodeAsync(User user, string email, string resetCode) =>
        transport.SendAsync(new OutgoingMail(email, EmailBodies.PasswordResetCodeSubject,
            EmailBodies.PasswordResetCodeBody(resetCode), "PasswordResetCode", user.Id));
}
