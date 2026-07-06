namespace TheCanalaveLibrary.Server;

/// <summary>
/// Configuration for <see cref="SmtpEmailSender"/>, bound from <c>Email</c>. Selected by
/// <c>Email:Provider = "Smtp"</c> (Program.cs provider switch; default is <c>NoOp</c>, which
/// keeps <see cref="IdentityNoOpEmailSender"/> registered instead — see cross-cutting.md
/// "Identity & Auth"). Under Aspire the AppHost injects the SMTP host/port at the Mailpit dev
/// inbox; in production they point at whichever transactional provider's SMTP endpoint is chosen
/// (decision row 8, `.claude/middle_plan_v2.md`) — every candidate provider (Postmark/SES/
/// Resend/SendGrid/Mailgun) exposes SMTP, so swapping providers is a config change only.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>The From address on outgoing mail (e.g. <c>noreply@thecanalavelibrary.com</c>).</summary>
    public string FromAddress { get; set; } = "";

    /// <summary>The From display name (e.g. "The Canalave Library").</summary>
    public string FromName { get; set; } = "";

    public EmailSmtpOptions Smtp { get; set; } = new();
}

/// <summary>SMTP transport settings, nested under <see cref="EmailOptions"/> (bound key <c>Email:Smtp</c>).</summary>
public sealed class EmailSmtpOptions
{
    public string Host { get; set; } = "";

    public int Port { get; set; } = 587;

    /// <summary>Null/empty for an unauthenticated relay (Mailpit's dev default).</summary>
    public string? User { get; set; }

    public string? Password { get; set; }

    /// <summary>STARTTLS on the given port. Mailpit's plain SMTP listener needs this false;
    /// a real provider's port 587 endpoint needs it true.</summary>
    public bool UseStartTls { get; set; } = true;
}
