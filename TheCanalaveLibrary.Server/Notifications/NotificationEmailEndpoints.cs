using System.Net;
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
/// <para><b>Raw HTML, not a Razor page</b>, through the shared <see cref="UnsubscribeLandingPage"/>
/// shell (extracted from this class at WU-PrivateMessageEmail, 2026-10-13, which also gave
/// private-message email its own route, <see cref="MessagingEmailEndpoints"/>). The shell's doc has
/// the reasoning. The token and the one action stay this feature's own (<c>security.md</c>
/// §"One-click unsubscribe tokens").</para>
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
                return InvalidLinkPage();
            }

            await using ReadOnlyApplicationDbContext readDb =
                await readDbFactory.CreateDbContextAsync(cancellationToken);

            NotificationSettingUpsert.UnsubscribeResult result = await NotificationSettingUpsert.UnsubscribeAsync(
                writeDb, readDb, userId, notifType, cancellationToken);

            if (result == NotificationSettingUpsert.UnsubscribeResult.UserGone)
            {
                // A validly signed link for an account deleted since the mail went out (tokens live a
                // year; mail clients and scanners re-POST old messages). The same page and the same
                // Information level as an expired link: routine, nothing to unsubscribe, and the page
                // does not reveal which of the two it was.
                logger.LogInformation(
                    "Unsubscribe token named user {UserId}, whose account no longer exists.", userId);
                return InvalidLinkPage();
            }

            if (result == NotificationSettingUpsert.UnsubscribeResult.UnknownType)
            {
                logger.LogWarning(
                    "Unsubscribe token carried unknown notification type {NotificationType} for user {UserId}.",
                    notifType, userId);
                return UnsubscribeLandingPage.Result(
                    "Something went wrong",
                    "<p>We couldn't apply that change.</p>",
                    StatusCodes.Status400BadRequest);
            }

            string typeName = await DisplayNameAsync(readDb, notifType, cancellationToken);
            logger.LogInformation(
                "Unsubscribed user {UserId} from {NotificationType} emails via one-click link.",
                userId, notifType);

            return UnsubscribeLandingPage.Result(
                "You're unsubscribed",
                $"<p>You will no longer receive <strong>{WebUtility.HtmlEncode(typeName)}</strong> emails.</p>" +
                "<p>You'll still see these notifications on the site.</p>");
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
                return InvalidLinkPage();
            }

            await using ReadOnlyApplicationDbContext readDb =
                await readDbFactory.CreateDbContextAsync(cancellationToken);
            string typeName = await DisplayNameAsync(readDb, notifType, cancellationToken);

            // The token is already URL-safe (base64url from Data Protection), but it is echoed into
            // markup here, so it is HTML-encoded like any other interpolated value.
            string action = WebUtility.HtmlEncode(UnsubscribeTokenService.PathFor(token));

            return UnsubscribeLandingPage.Result(
                "Unsubscribe",
                $"<p>Stop receiving <strong>{WebUtility.HtmlEncode(typeName)}</strong> emails?</p>" +
                $"<form method=\"post\" action=\"{action}\">" +
                "<button type=\"submit\">Unsubscribe</button></form>" +
                "<p>You'll still see these notifications on the site.</p>");
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
    /// The one "link no longer valid" answer (400): a tampered, expired or rotated-key token, or a
    /// valid token whose account has since been deleted — deliberately indistinguishable.
    /// </summary>
    private static IResult InvalidLinkPage() =>
        UnsubscribeLandingPage.Result(
            "Link no longer valid",
            "<p>This unsubscribe link has expired or is not valid.</p>" +
            "<p>You can still manage every notification setting from your account.</p>",
            StatusCodes.Status400BadRequest);
}
