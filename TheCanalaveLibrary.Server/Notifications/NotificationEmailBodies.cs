using System.Net;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Subject/body composition for notification email (WU-NotifEmail). Pure functions — no send logic,
/// no DI — so the exact rendered shape is unit-testable without a live SMTP connection, exactly as
/// <see cref="EmailBodies"/> is for Identity's transactional mail.
///
/// <para><b>Message text comes from <see cref="NotificationPresenter.Compose"/></b>, the same
/// presenter the in-app notification panel uses. That switch has an arm per notification type and
/// is already unit-tested (<c>NotificationPresenterTests</c>); forking it for email would guarantee
/// the two surfaces drift as new types are minted. Only the text is used here — the icon SVG path
/// and accent colour it also returns have no meaning in email.</para>
///
/// <para><b>Deliberate design-system exception.</b> These bodies are table-based HTML with inline
/// styles and literal hex colours — the opposite of <c>layer4-style.md</c>'s element-role rules.
/// That is required, not sloppy: email clients strip <c>&lt;style&gt;</c> blocks, do not support CSS
/// custom properties (so every design token would resolve to nothing), and Outlook's Word rendering
/// engine ignores most modern layout. <c>scripts/check-design-tokens.ps1</c> scans <c>.razor</c>
/// markup and does not see this file; this paragraph is why that is correct rather than an
/// oversight, and a future audit should not "fix" it.</para>
///
/// <para><b>Every interpolated value is HTML-encoded here.</b> Story titles, chapter titles, group
/// names and usernames are all user-supplied and arrive raw from
/// <see cref="NotificationEnricher"/> — unlike <see cref="EmailBodies"/>, whose callback links
/// arrive pre-encoded from the Identity scaffold. The two files therefore have opposite encoding
/// contracts; do not copy one's rule into the other.</para>
/// </summary>
public static class NotificationEmailBodies
{
    /// <summary>Site-relative route of the notification settings page, for the "manage" footer link.</summary>
    public const string SettingsPath = "/notifications/settings";

    // Literal colours, not design tokens — see the class doc's design-system exception.
    private const string InkColor = "#1f2933";
    private const string MutedInkColor = "#6b7280";
    private const string AccentColor = "#3b5bdb";
    private const string RuleColor = "#e5e7eb";

    /// <summary>
    /// Composes one notification email.
    /// </summary>
    /// <param name="notification">The enriched notification, as the in-app panel would receive it.</param>
    /// <param name="typeDisplayName">
    /// <c>NotificationType.DisplayName</c> (e.g. "New Chapter", "Account Warning") — seeded per type
    /// and already written as a short human-readable label, which is exactly what a subject line
    /// needs. Also names the type in the unsubscribe footer, so the reader knows precisely what the
    /// link silences.
    /// </param>
    /// <param name="absoluteTargetUrl">
    /// Absolute deep link to the related entity, or null for types with no navigable target (site
    /// announcements, account warnings, report outcomes). Null suppresses the call-to-action button
    /// rather than rendering a dead one.
    /// </param>
    /// <param name="absoluteUnsubscribeUrl">Absolute one-click unsubscribe URL for this type.</param>
    /// <param name="absoluteSettingsUrl">Absolute URL of the notification settings page.</param>
    public static (string Subject, string HtmlBody) Compose(
        NotificationDto notification,
        string typeDisplayName,
        string? absoluteTargetUrl,
        string absoluteUnsubscribeUrl,
        string absoluteSettingsUrl)
    {
        (string text, _, _) = NotificationPresenter.Compose(notification);

        string safeText = WebUtility.HtmlEncode(text);
        string safeTypeName = WebUtility.HtmlEncode(typeDisplayName);

        // The target title is already inside `text`; the button just needs a generic verb, so no
        // second encoded copy of the title is required here.
        string callToAction = absoluteTargetUrl is null
            ? ""
            : $"""
               <p style="margin:0 0 24px">
                 <a href="{WebUtility.HtmlEncode(absoluteTargetUrl)}"
                    style="color:{AccentColor};font-weight:600;text-decoration:underline">View it on the site</a>
               </p>
               """;

        string body = $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"
                   style="background:#ffffff">
              <tr>
                <td align="center" style="padding:24px 12px">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"
                         style="max-width:520px;font-family:Georgia,'Times New Roman',serif;color:{InkColor};font-size:16px;line-height:1.6">
                    <tr>
                      <td style="padding-bottom:16px;font-size:13px;letter-spacing:.08em;text-transform:uppercase;color:{MutedInkColor}">
                        The Canalave Library
                      </td>
                    </tr>
                    <tr>
                      <td>
                        <p style="margin:0 0 20px">{safeText}</p>
                        {callToAction}
                      </td>
                    </tr>
                    <tr>
                      <td style="border-top:1px solid {RuleColor};padding-top:16px;font-size:13px;line-height:1.5;color:{MutedInkColor}">
                        <p style="margin:0 0 8px">
                          You received this because "{safeTypeName}" emails are on for your account.
                        </p>
                        <p style="margin:0">
                          <a href="{WebUtility.HtmlEncode(absoluteUnsubscribeUrl)}" style="color:{MutedInkColor}">Unsubscribe from these</a>
                          &nbsp;·&nbsp;
                          <a href="{WebUtility.HtmlEncode(absoluteSettingsUrl)}" style="color:{MutedInkColor}">All notification settings</a>
                        </p>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>
            """;

        return (typeDisplayName, body);
    }
}
