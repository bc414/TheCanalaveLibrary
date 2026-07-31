using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// The unsubscribe surface for notification email (WU-NotifEmail). Two routes over one token:
///
/// <list type="bullet">
///   <item><b><c>POST /unsubscribe/{token}</c></b> — the RFC 8058 one-click target named by the
///   <c>List-Unsubscribe-Post</c> header. Mail clients POST it directly, with no cookies and from
///   an unrelated IP, so it is anonymous and antiforgery-exempt by necessity; the signed token
///   <em>is</em> the credential.</item>
///   <item><b><c>GET /unsubscribe/{token}</c></b> — what the visible footer link opens. It does
///   <b>not</b> unsubscribe. It renders a confirmation page whose button POSTs to the route above.
///   That split is deliberate: corporate link-scanners and mail-preview crawlers follow every GET
///   in a message, and a GET that mutated state would silently unsubscribe users who never clicked
///   anything.</item>
/// </list>
///
/// <para><b>Why raw HTML instead of a Razor page.</b> These pages must render for a signed-out
/// visitor arriving cold from a mail client, and must keep working when the interactive stack does
/// not — including right now, with the Identity funnel returning 500s (tracker H10). A
/// self-contained response has no render mode, no circuit, no auth dependency, and no layout to
/// fail. The trade is that it does not carry the site's design system; that is an accepted,
/// deliberate exception for two pages, revisitable once H10 is fixed. <c>check-design-tokens.ps1</c>
/// governs <c>.razor</c> markup and does not see this file — that is not a loophole being exploited,
/// it is why the exception is written down here.</para>
///
/// <para><b>Rate limited</b> via the "Unsubscribe" policy (Program.cs): the token space is not
/// enumerable, but an unauthenticated endpoint that does database writes should never be
/// unbounded.</para>
/// </summary>
public static class NotificationEmailEndpoints
{
    public static WebApplication MapNotificationEmailEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/unsubscribe")
            .AllowAnonymous()
            .RequireRateLimiting("Unsubscribe");

        // One-click target (RFC 8058). No antiforgery token: the POST originates in a mail client
        // that has never seen this site and cannot hold one.
        group.MapPost("/{token}", async (
            string token,
            UnsubscribeTokenService tokens,
            ApplicationDbContext writeDb,
            IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            ILogger logger = loggerFactory.CreateLogger(typeof(NotificationEmailEndpoints));

            if (tokens.TryRead(token) is not (int userId, NotificationTypeEnum notifType))
            {
                // Deliberately does not distinguish tampered / expired / rotated-key — see
                // UnsubscribeTokenService.TryRead. Logged at Information, not Warning: stale links
                // and link-scanner traffic make this a routine outcome, not an incident.
                logger.LogInformation("Rejected an invalid or expired unsubscribe token.");
                return Results.Content(Page(
                    "Link no longer valid",
                    "<p>This unsubscribe link has expired or is not valid.</p>" +
                    "<p>You can still manage every notification setting from your account.</p>",
                    settingsLink: true), "text/html", Encoding.UTF8, StatusCodes.Status400BadRequest);
            }

            await using ReadOnlyApplicationDbContext readDb =
                await readDbFactory.CreateDbContextAsync(cancellationToken);

            bool applied = await NotificationSettingUpsert.UnsubscribeAsync(
                writeDb, readDb, userId, notifType, cancellationToken);

            if (!applied)
            {
                logger.LogWarning(
                    "Unsubscribe token carried unknown notification type {NotificationType} for user {UserId}.",
                    notifType, userId);
                return Results.Content(Page(
                    "Something went wrong",
                    "<p>We couldn't apply that change.</p>",
                    settingsLink: true), "text/html", Encoding.UTF8, StatusCodes.Status400BadRequest);
            }

            string typeName = await DisplayNameAsync(readDb, notifType, cancellationToken);
            logger.LogInformation(
                "Unsubscribed user {UserId} from {NotificationType} emails via one-click link.",
                userId, notifType);

            return Results.Content(Page(
                "You're unsubscribed",
                $"<p>You will no longer receive <strong>{WebUtility.HtmlEncode(typeName)}</strong> emails.</p>" +
                "<p>You'll still see these notifications on the site.</p>",
                settingsLink: true), "text/html", Encoding.UTF8);
        }).DisableAntiforgery();

        // Human-facing confirmation. Read-only by design — see the class doc.
        group.MapGet("/{token}", async (
            string token,
            UnsubscribeTokenService tokens,
            IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
            CancellationToken cancellationToken) =>
        {
            if (tokens.TryRead(token) is not (int _, NotificationTypeEnum notifType))
            {
                return Results.Content(Page(
                    "Link no longer valid",
                    "<p>This unsubscribe link has expired or is not valid.</p>" +
                    "<p>You can still manage every notification setting from your account.</p>",
                    settingsLink: true), "text/html", Encoding.UTF8, StatusCodes.Status400BadRequest);
            }

            await using ReadOnlyApplicationDbContext readDb =
                await readDbFactory.CreateDbContextAsync(cancellationToken);
            string typeName = await DisplayNameAsync(readDb, notifType, cancellationToken);

            // The token is already URL-safe (base64url from Data Protection), but it is echoed into
            // markup here, so it is HTML-encoded like any other interpolated value.
            string action = WebUtility.HtmlEncode(UnsubscribeTokenService.PathFor(token));

            return Results.Content(Page(
                "Unsubscribe",
                $"<p>Stop receiving <strong>{WebUtility.HtmlEncode(typeName)}</strong> emails?</p>" +
                $"<form method=\"post\" action=\"{action}\">" +
                "<button type=\"submit\">Unsubscribe</button></form>" +
                "<p>You'll still see these notifications on the site.</p>",
                settingsLink: true), "text/html", Encoding.UTF8);
        });

        return app;
    }

    private static async Task<string> DisplayNameAsync(
        ReadOnlyApplicationDbContext readDb, NotificationTypeEnum notifType, CancellationToken cancellationToken) =>
        await readDb.NotificationTypes
            .Where(t => t.NotificationTypeId == notifType)
            .Select(t => t.DisplayName)
            .FirstOrDefaultAsync(cancellationToken) ?? "these";

    /// <summary>
    /// Minimal self-contained HTML shell. Inline styles only — this page must render identically
    /// whether or not the app's stylesheet loads, and it is never composed with site layout.
    /// </summary>
    private static string Page(string title, string bodyHtml, bool settingsLink) =>
        $"""
         <!doctype html>
         <html lang="en">
         <head>
         <meta charset="utf-8">
         <meta name="viewport" content="width=device-width,initial-scale=1">
         <meta name="robots" content="noindex,nofollow">
         <title>{WebUtility.HtmlEncode(title)} — The Canalave Library</title>
         </head>
         <body style="font-family:system-ui,sans-serif;line-height:1.6;max-width:34rem;margin:4rem auto;padding:0 1rem">
         <h1 style="font-size:1.4rem">{WebUtility.HtmlEncode(title)}</h1>
         {bodyHtml}
         {(settingsLink ? "<p><a href=\"/settings\">Manage all notification settings</a></p>" : "")}
         <p><a href="/">Return to The Canalave Library</a></p>
         </body>
         </html>
         """;
}
