namespace TheCanalaveLibrary.Server;

/// <summary>
/// The <c>Email:Provider = "NoOp"</c> (default) branch's <see cref="IMailTransport"/>: logs what it
/// would have sent and drops it. Mirrors <see cref="IdentityNoOpEmailSender"/>'s role on the
/// Identity side, and keeps every mail-producing code path resolvable in a host that has no SMTP
/// configuration — the server-only run path, and the integration-test host.
///
/// <para>Registering this instead of <see cref="SmtpMailTransport"/> is also what stops
/// <see cref="NotificationEmailWorker"/> from being registered at all (Program.cs), so an
/// unconfigured host does no drain work rather than draining into a sink.</para>
/// </summary>
public sealed class NoOpMailTransport(ILogger<NoOpMailTransport> logger) : IMailTransport
{
    public Task SendAsync(OutgoingMail mail, CancellationToken cancellationToken = default) =>
        SendBatchAsync([mail], cancellationToken);

    public Task SendBatchAsync(IReadOnlyList<OutgoingMail> batch, CancellationToken cancellationToken = default)
    {
        foreach (OutgoingMail mail in batch)
        {
            // Address deliberately not logged even here (logging.md "What NOT to log") — the NoOp
            // path runs in dev, but the rule is about not teaching the habit.
            logger.LogInformation(
                "Email suppressed (no mail provider configured): {EmailKind} to user {UserId}, subject {Subject}",
                mail.Kind, mail.UserId, mail.Subject);
        }

        return Task.CompletedTask;
    }
}
