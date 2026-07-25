using System.Security.Cryptography;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Pure code-generation transform for Feature 53's per-user public verification code (WU39,
/// audit/Moderation.md F53), extracted so it's unit-testable with no DbContext — mirrors
/// <see cref="StorySlug"/>. Uniqueness enforcement (retry-on-collision against the DB's filtered
/// unique index) stays server-side; see <c>ServerExternalVerificationWriteService.EnsureMyVerificationCodeAsync</c>.
///
/// The code is a public nonce, not a secret: it is displayed at the bottom of the user's own TCL
/// profile, and the author places the same text on each external platform profile they verify.
/// Publishing it has no security downside — producing a match requires controlling the external
/// profile, which is exactly the fact being proven.
/// </summary>
public static class PublicVerificationCode
{
    private const string Prefix = "TCL-Verify-";

    // Unambiguous base32 alphabet — no 0/O, 1/I/L confusion when an author is transcribing this
    // by hand onto an external site.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string New()
    {
        Span<char> chars = stackalloc char[6];
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return Prefix + new string(chars);
    }
}
