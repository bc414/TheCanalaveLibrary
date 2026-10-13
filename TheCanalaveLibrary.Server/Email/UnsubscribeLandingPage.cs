using System.Net;
using System.Text;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// The self-contained HTML shell every one-click unsubscribe page renders through
/// (<see cref="NotificationEmailEndpoints"/>, <see cref="MessagingEmailEndpoints"/>). Extracted from
/// the notification endpoints at WU-PrivateMessageEmail (2026-10-13), when a second owner of an
/// unsubscribe route arrived. Presentation infrastructure only: each feature keeps its own token
/// service and its own one action (<c>security.md</c> §"One-click unsubscribe tokens" — "shared
/// convention, not shared code", owner ruling D18).
///
/// <para><b>Why raw HTML instead of a Razor page.</b> These pages must render for a signed-out
/// visitor arriving cold from a mail client, and must keep working when the interactive stack does
/// not. A self-contained response has no render mode, no circuit, no auth dependency, and no layout
/// to fail. That is not a hypothetical: tracker H10 had every layout-bearing static-SSR page —
/// the whole Identity funnel — returning a raw 500 for eighteen days (2026-07-13 → 07-31) because
/// one chrome component in <c>MainLayout</c> registered persistent state with no render mode to
/// infer. These pages would have been immune. The trade is that they do not carry the site's design
/// system; that is an accepted, deliberate exception for a handful of pages. Revisit only if they
/// ever need to look like the site — the isolation is the feature, not a temporary workaround.
/// <c>check-design-tokens.ps1</c> governs <c>.razor</c> markup and does not see this file — that is
/// not a loophole being exploited, it is why the exception is written down here.</para>
/// </summary>
public static class UnsubscribeLandingPage
{
    /// <summary>
    /// Site-relative route of the one page that holds every optional email setting (D18's standing
    /// rider), linked from every landing page.
    /// </summary>
    public const string EmailSettingsPath = "/notifications/settings";

    /// <summary>
    /// Renders a complete page. <paramref name="bodyHtml"/> is inserted as-is, so the caller must
    /// already have HTML-encoded every value it interpolated; the title is encoded here.
    /// </summary>
    public static string Render(string title, string bodyHtml) =>
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
         <p><a href="{EmailSettingsPath}">Manage your email settings</a></p>
         <p><a href="/">Return to The Canalave Library</a></p>
         </body>
         </html>
         """;

    /// <summary>The page as an HTML result with the given status (200 unless stated).</summary>
    public static IResult Result(string title, string bodyHtml, int statusCode = StatusCodes.Status200OK) =>
        Results.Content(Render(title, bodyHtml), "text/html", Encoding.UTF8, statusCode);
}
