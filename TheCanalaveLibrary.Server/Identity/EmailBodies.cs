using System.Net;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// HTML subject/body composition for <see cref="SmtpEmailSender"/>'s three Identity transactional
/// emails. Pulled out as pure functions (no send logic, no DI) so the exact rendered shape is
/// unit-testable without a live SMTP connection.
///
/// <b><c>confirmationLink</c>/<c>resetLink</c> arrive already HTML-encoded — do not encode them
/// again.</b> Every caller (<c>Register.razor</c>, <c>ForgotPassword.razor</c>,
/// <c>ResendEmailConfirmation.razor</c>, <c>ExternalLogin.razor</c>, <c>Manage/Email.razor</c>)
/// wraps the callback URL in <c>HtmlEncoder.Default.Encode(...)</c> before calling
/// <c>IEmailSender&lt;User&gt;</c> — that's the framework-scaffold contract, and
/// <c>IdentityNoOpEmailSender</c> interpolates the link verbatim on that assumption too.
/// Re-encoding here turns the link's <c>&amp;amp;</c> (already-escaped <c>&amp;</c> query
/// separator) into <c>&amp;amp;amp;</c> — a real bug found via live Mailpit verification
/// (2026-07-06): the double-escaped separator survives one round of browser HTML-decoding as
/// literal text instead of a real <c>&amp;</c>, so the query string parser never sees a
/// second parameter and <c>code</c> fails to bind on <c>ConfirmEmail</c>/<c>ResetPassword</c>.
/// <c>resetCode</c> (the one caller-agnostic value — no Razor page in this codebase calls
/// <see cref="SmtpEmailSender.SendPasswordResetCodeAsync"/>; it exists for the Identity API
/// endpoints' JSON clients) is NOT pre-encoded by any caller, so it alone is HTML-encoded here.
/// </summary>
public static class EmailBodies
{
    public const string ConfirmationSubject = "Confirm your email";

    public static string ConfirmationBody(string confirmationLink) =>
        $"<p>Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.</p>";

    public const string PasswordResetLinkSubject = "Reset your password";

    public static string PasswordResetLinkBody(string resetLink) =>
        $"<p>Please reset your password by <a href='{resetLink}'>clicking here</a>.</p>";

    public const string PasswordResetCodeSubject = "Reset your password";

    public static string PasswordResetCodeBody(string resetCode) =>
        $"<p>Please reset your password using the following code: {WebUtility.HtmlEncode(resetCode)}</p>";
}
