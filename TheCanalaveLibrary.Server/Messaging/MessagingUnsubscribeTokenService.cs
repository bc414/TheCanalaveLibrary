using Microsoft.AspNetCore.DataProtection;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Mints and validates the signed tokens behind the one-click unsubscribe link in private-message
/// email (owner ruling D18, WU-PrivateMessageEmail 2026-10-13). A token authorises exactly one action
/// — "turn <c>User.EmailOnNewMessage</c> off for this user" — and nothing else.
///
/// <para><b>A separate class, not a shared base with <see cref="UnsubscribeTokenService"/>.</b> D18
/// makes the unsubscribe pattern a shared <em>convention</em>, not shared code (<c>security.md</c>
/// §"One-click unsubscribe tokens"): each owning feature has its own Data Protection purpose, so a
/// notification link can never silence message email and a message link can never touch a
/// notification setting. The two classes deliberately look alike.</para>
///
/// <para><b>Scope discipline:</b> the payload is the user id alone, never a session or an auth
/// ticket. A leaked token can turn one user's new-message email off — annoying, not dangerous — and
/// can never read data, sign in, or enable anything.</para>
/// </summary>
public sealed class MessagingUnsubscribeTokenService
{
    /// <summary>
    /// Data Protection purpose string. Versioned: bumping the suffix invalidates every outstanding
    /// message-email unsubscribe link at once, which is the intended lever if the payload ever changes.
    /// </summary>
    private const string Purpose = "TheCanalaveLibrary.MessagingUnsubscribe.v1";

    /// <summary>
    /// How long a mailed link stays valid — the same year as the notification links: mail sits in
    /// inboxes for months, and an expired unsubscribe link is a worse outcome than a long-lived one
    /// whose worst case is muting one email.
    /// </summary>
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(365);

    private readonly ITimeLimitedDataProtector _protector;

    public MessagingUnsubscribeTokenService(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector(Purpose).ToTimeLimitedDataProtector();

    /// <summary>The site-relative path a token resolves to. Two segments, so it never collides with
    /// the notification group's one-segment <c>/unsubscribe/{token}</c>.</summary>
    public static string PathFor(string token) => $"/unsubscribe/messages/{token}";

    /// <summary>
    /// Mints a token for one user. Base64url (Data Protection's string protector), so it is safe in a
    /// URL path unescaped.
    /// </summary>
    public string CreateToken(int userId) =>
        _protector.Protect(userId.ToString(System.Globalization.CultureInfo.InvariantCulture), TokenLifetime);

    /// <summary>
    /// Validates and decodes a token. Returns <c>null</c> for anything not currently valid —
    /// tampered, truncated, expired, minted under another purpose (a notification token included),
    /// or unprotectable because the key ring rotated away. Callers must treat all of those
    /// identically and must never surface which one occurred.
    /// </summary>
    public int? TryRead(string token)
    {
        string payload;
        try
        {
            payload = _protector.Unprotect(token);
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or FormatException)
        {
            // Sanctioned silent catch (logging.md §"No Silent Catches"): an invalid token is the
            // expected steady-state outcome for link scanners and stale mail, not an error
            // condition. The endpoint logs the rejection once with request context; logging here
            // too would double-log and would make routine scanner traffic look like incidents.
            return null;
        }

        return int.TryParse(payload, System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out int userId) && userId > 0
            ? userId
            : null;
    }
}
