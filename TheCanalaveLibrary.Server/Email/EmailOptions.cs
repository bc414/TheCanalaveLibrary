namespace TheCanalaveLibrary.Server;

/// <summary>
/// Configuration for <see cref="SmtpMailTransport"/>, bound from <c>Email</c>. Selected by
/// <c>Email:Provider = "Smtp"</c> (Program.cs provider switch; default is <c>NoOp</c>, which
/// registers <see cref="NoOpMailTransport"/> and <see cref="IdentityNoOpEmailSender"/> instead —
/// see cross-cutting.md "Identity &amp; Auth"). Under Aspire the AppHost injects the SMTP host/port
/// at the Mailpit dev inbox; in production they point at whichever transactional provider's SMTP
/// endpoint is chosen (decision row 8, `.claude/roadmap.md`) — every candidate provider
/// (Postmark/SES/Resend/SendGrid/Mailgun) exposes SMTP, so swapping providers is a config change only.
///
/// <para><b>Moved from <c>Server/Identity/</c> to <c>Server/Email/</c> at WU-NotifEmail
/// (2026-07-31)</b>, when notification fan-out became a second consumer: this is no longer
/// Identity's config, it is the app's outbound-mail config. The namespace is unchanged (one flat
/// namespace per project), so the move is path-only.</para>
///
/// <para><b>Note there is no email-specific base-URL setting here.</b> Absolute links in mail come
/// from <see cref="Core.IPublicUrlProvider"/> / <c>Site:PublicBaseUrl</c> — the same configured
/// canonical origin Open Graph tags use, and for the same reason (never derive it from the current
/// request). Do not add one.</para>
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
