using System.Net;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Subject/body composition for private-message email (owner ruling D18: Messaging owns the body;
/// WU-PrivateMessageEmail 2026-10-13). Pure functions — no send logic, no DI — so the exact rendered
/// shape is unit-testable without a live SMTP connection, exactly as <see cref="NotificationEmailBodies"/>
/// is for notification mail.
///
/// <para><b>No sender-authored text, by construction.</b> <see cref="Compose"/> has no parameter for the
/// message or the conversation subject (which allows 2048 characters), so neither can reach a mail.
/// Otherwise anyone able to start a conversation could have the site deliver arbitrary text to any
/// confirmed address — a spam relay — and a message's privacy would rest on the recipient's mail
/// provider. A derived choice awaiting the owner's confirmation (<c>roadmap.md</c> decision row 28
/// (b)).</para>
///
/// <para><b>Deliberate design-system exception.</b> These bodies are table-based HTML with inline
/// styles and literal hex colours — the opposite of <c>layer4-style.md</c>'s element-role rules.
/// That is required, not sloppy: email clients strip <c>&lt;style&gt;</c> blocks, do not support CSS
/// custom properties (so every design token would resolve to nothing), and Outlook's Word rendering
/// engine ignores most modern layout. <c>scripts/check-design-tokens.ps1</c> scans <c>.razor</c>
/// markup and does not see this file; this paragraph is why that is correct rather than an
/// oversight, and a future audit should not "fix" it.</para>
///
/// <para><b>Every interpolated value is HTML-encoded here</b>: the sender's username is
/// user-chosen, and the URLs are encoded because they land in attributes.</para>
/// </summary>
public static class PrivateMessageEmailBodies
{
    /// <summary>What a mail calls a sender whose account is gone (or whose name is unknown).</summary>
    public const string UnknownSender = "a member";

    // Literal colours, not design tokens — see the class doc's design-system exception. The same
    // palette as NotificationEmailBodies, so the site's mail looks like one family.
    private const string InkColor = "#1f2933";
    private const string MutedInkColor = "#6b7280";
    private const string AccentColor = "#3b5bdb";
    private const string RuleColor = "#e5e7eb";

    /// <summary>Composes one new-message email.</summary>
    /// <param name="senderUserName">The sender's username, or null when the account was deleted.</param>
    /// <param name="absoluteConversationUrl">Absolute link to <c>/messages/{conversationId}</c>.</param>
    /// <param name="absoluteUnsubscribeUrl">Absolute one-click unsubscribe URL for message email.</param>
    /// <param name="absoluteSettingsUrl">Absolute URL of the email settings page.</param>
    public static (string Subject, string HtmlBody) Compose(
        string? senderUserName,
        string absoluteConversationUrl,
        string absoluteUnsubscribeUrl,
        string absoluteSettingsUrl)
    {
        string sender = string.IsNullOrWhiteSpace(senderUserName) ? UnknownSender : senderUserName;
        string safeSender = WebUtility.HtmlEncode(sender);

        // A subject line is plain text, not HTML: it is not encoded (the transport writes it as a
        // MIME header). Only the body interpolates into markup.
        string subject = $"New private message from {sender}";

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
                        <p style="margin:0 0 20px">{safeSender} sent you a private message on The Canalave Library.</p>
                        <p style="margin:0 0 24px">
                          <a href="{WebUtility.HtmlEncode(absoluteConversationUrl)}"
                             style="color:{AccentColor};font-weight:600;text-decoration:underline">Read it on the site</a>
                        </p>
                      </td>
                    </tr>
                    <tr>
                      <td style="border-top:1px solid {RuleColor};padding-top:16px;font-size:13px;line-height:1.5;color:{MutedInkColor}">
                        <p style="margin:0 0 8px">
                          You received this because new-message emails are on for your account. You won't get
                          another until you've read this conversation.
                        </p>
                        <p style="margin:0">
                          <a href="{WebUtility.HtmlEncode(absoluteUnsubscribeUrl)}" style="color:{MutedInkColor}">Unsubscribe from message emails</a>
                          &nbsp;·&nbsp;
                          <a href="{WebUtility.HtmlEncode(absoluteSettingsUrl)}" style="color:{MutedInkColor}">All email settings</a>
                        </p>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>
            """;

        return (subject, body);
    }
}
