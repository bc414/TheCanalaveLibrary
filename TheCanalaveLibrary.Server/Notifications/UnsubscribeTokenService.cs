using Microsoft.AspNetCore.DataProtection;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Mints and validates the signed tokens behind one-click unsubscribe links in notification email
/// (WU-NotifEmail). A token authorises exactly one action — "turn <c>EmailEnabled</c> off for this
/// user and this notification type" — and nothing else.
///
/// <para><b>Why a token rather than a login-gated settings link:</b> RFC 8058 one-click unsubscribe
/// is what Gmail's and Yahoo's bulk-sender rules expect, and the mail client POSTs the URL with no
/// cookies, from an IP unrelated to the user. A signed token is the only thing that can carry
/// identity in that request. The visible footer link still exists for humans; both point here.</para>
///
/// <para><b>No schema, no migration, no package.</b> ASP.NET Data Protection is already in the
/// container via Identity — this only names a purpose over it. Key-ring rules (persistence,
/// rotation, the multi-node story) are unchanged and live in <c>security.md</c> §"Data Protection";
/// note that a key-ring loss invalidates outstanding unsubscribe links, which degrade to "token no
/// longer valid, manage your settings here" rather than to a wrong action.</para>
///
/// <para><b>Scope discipline:</b> the payload carries the user id and the notification type, never
/// a session or an auth ticket. A leaked or brute-forced token can silence one notification type
/// for one user — annoying, not dangerous — and can never read data, sign in, or enable anything.
/// Unsubscribing is deliberately the only direction this token can move a setting.</para>
/// </summary>
public sealed class UnsubscribeTokenService
{
    /// <summary>
    /// Data Protection purpose string. Versioned: bumping the suffix invalidates every outstanding
    /// token at once, which is the intended lever if the payload format ever changes.
    /// </summary>
    private const string Purpose = "TheCanalaveLibrary.NotificationUnsubscribe.v1";

    /// <summary>
    /// How long a mailed unsubscribe link stays valid. Generous on purpose — mail sits in inboxes
    /// for months, and an expired unsubscribe link is a worse outcome than a long-lived one whose
    /// worst case is muting one notification type.
    /// </summary>
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(365);

    private readonly ITimeLimitedDataProtector _protector;

    public UnsubscribeTokenService(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector(Purpose).ToTimeLimitedDataProtector();

    /// <summary>The site-relative path an unsubscribe token resolves to.</summary>
    public static string PathFor(string token) => $"/unsubscribe/{token}";

    /// <summary>
    /// Mints a token for one (user, notification type) pair. The result is base64url-encoded by
    /// Data Protection's string protector, so it is safe to embed in a URL path unescaped.
    /// </summary>
    public string CreateToken(int userId, NotificationTypeEnum notificationType) =>
        _protector.Protect($"{userId}:{(int)notificationType}", TokenLifetime);

    /// <summary>
    /// Validates and decodes a token. Returns <c>null</c> for anything not currently valid —
    /// tampered, truncated, expired, minted under a different purpose, or unprotectable because the
    /// key ring rotated away. Callers must treat all of those identically and must never surface
    /// which one occurred (that distinction is only useful to someone probing the endpoint).
    /// </summary>
    public (int UserId, NotificationTypeEnum NotificationType)? TryRead(string token)
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

        string[] parts = payload.Split(':');
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], out int userId) ||
            !int.TryParse(parts[1], out int typeId) ||
            !Enum.IsDefined(typeof(NotificationTypeEnum), (NotificationTypeEnum)typeId))
        {
            return null;
        }

        return (userId, (NotificationTypeEnum)typeId);
    }
}
