using System.Net;
using Microsoft.EntityFrameworkCore;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// The unsubscribe surface for private-message email (owner ruling D18, WU-PrivateMessageEmail
/// 2026-10-13). Two routes over one <see cref="MessagingUnsubscribeTokenService"/> token, the same
/// shape as the notification routes (<see cref="NotificationEmailEndpoints"/>) because both follow one
/// convention (<c>security.md</c> §"One-click unsubscribe tokens"), not because they share code:
///
/// <list type="bullet">
///   <item><b><c>POST /unsubscribe/messages/{token}</c></b> — the RFC 8058 one-click target named by
///   the <c>List-Unsubscribe-Post</c> header. Mail clients POST it with no cookies, from an unrelated
///   IP, so it is anonymous and antiforgery-exempt by necessity; the signed token is the credential.
///   Its one action turns <c>User.EmailOnNewMessage</c> off. It can never turn it on, and a repeat
///   answers the same page.</item>
///   <item><b><c>GET /unsubscribe/messages/{token}</c></b> — what the visible footer link opens. It
///   changes nothing: it renders a confirmation form that POSTs to the route above, because link
///   scanners and mail-preview crawlers follow every GET in a message.</item>
/// </list>
///
/// <para>A tampered, expired, rotated-key or foreign-purpose token (a notification token included),
/// and a valid token whose account has since been deleted, all answer the same 400 "no longer valid"
/// page — the notification routes' rule (WU-NotificationCorrectness review fixes, 2026-10-13), so the
/// page never reveals whether an account exists. Rate limited by the shared per-IP "Unsubscribe"
/// policy (Program.cs). Pages render through <see cref="UnsubscribeLandingPage"/>.</para>
/// </summary>
public static class MessagingEmailEndpoints
{
    public static WebApplication MapMessagingEmailEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/unsubscribe/messages")
            .AllowAnonymous()
            .RequireRateLimiting("Unsubscribe");

        // One-click target (RFC 8058). No antiforgery token: the POST originates in a mail client
        // that has never seen this site and cannot hold one.
        group.MapPost("/{token}", async (
            string token,
            MessagingUnsubscribeTokenService tokens,
            ApplicationDbContext writeDb,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            ILogger logger = loggerFactory.CreateLogger(typeof(MessagingEmailEndpoints));

            if (tokens.TryRead(token) is not int userId)
            {
                // Deliberately does not distinguish tampered / expired / rotated-key / wrong purpose.
                // Information, not Warning: stale links and link-scanner traffic make this routine.
                logger.LogInformation("Rejected an invalid or expired message-email unsubscribe token.");
                return InvalidLinkPage();
            }

            // The one action: off, never on. Set-based, so nothing is tracked and a re-POST is a
            // harmless rewrite of the same value.
            int updated = await writeDb.Users
                .Where(u => u.Id == userId)
                .ExecuteUpdateAsync(set => set.SetProperty(u => u.EmailOnNewMessage, false), cancellationToken);

            if (updated == 0)
            {
                // A validly signed link for an account deleted since the mail went out. Same page and
                // level as an expired link: nothing to unsubscribe, and nothing revealed.
                logger.LogInformation(
                    "Message-email unsubscribe token named user {UserId}, whose account no longer exists.", userId);
                return InvalidLinkPage();
            }

            logger.LogInformation("Unsubscribed user {UserId} from new-message emails via one-click link.", userId);
            return UnsubscribeLandingPage.Result(
                "You're unsubscribed",
                "<p>You will no longer receive an email when someone sends you a private message.</p>" +
                "<p>Your messages are still waiting on the site, and you can turn these emails back on " +
                "from your email settings.</p>");
        }).DisableAntiforgery();

        // Human-facing confirmation. Read-only by design — see the class doc.
        group.MapGet("/{token}", (string token, MessagingUnsubscribeTokenService tokens) =>
        {
            if (tokens.TryRead(token) is null)
                return InvalidLinkPage();

            // The token is already URL-safe (base64url), but it is echoed into markup, so it is
            // HTML-encoded like any other interpolated value.
            string action = WebUtility.HtmlEncode(MessagingUnsubscribeTokenService.PathFor(token));

            return UnsubscribeLandingPage.Result(
                "Unsubscribe",
                "<p>Stop receiving an email when someone sends you a private message?</p>" +
                $"<form method=\"post\" action=\"{action}\">" +
                "<button type=\"submit\">Unsubscribe</button></form>" +
                "<p>Your messages will still arrive on the site.</p>");
        });

        return app;
    }

    /// <summary>
    /// The one "link no longer valid" answer (400) — deliberately the same for every invalid reason,
    /// including a valid token whose account is gone.
    /// </summary>
    private static IResult InvalidLinkPage() =>
        UnsubscribeLandingPage.Result(
            "Link no longer valid",
            "<p>This unsubscribe link has expired or is not valid.</p>" +
            "<p>You can still turn message emails off from your email settings.</p>",
            StatusCodes.Status400BadRequest);
}
