using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// The anonymous viewer's prefs cookie (WU-AccessGate; identity-and-authorization.md §"Viewer
/// Consent State"): the global mature toggle plus per-item reveals, for viewers with no account
/// row to store them in. First-party functional cookie set only by explicit user action (the
/// interstitial / a settings toggle) — no consent banner applies (settled 2026-07-19; noted for
/// the row-10 privacy policy).
///
/// Cookie attributes (see <see cref="Append"/>):
/// - SameSite=Lax — MUST ride inbound cross-site top-level navigations (a Discord/search link to
///   a revealed story); Strict would re-gate every external arrival.
/// - NOT HttpOnly — contents are non-sensitive viewer preferences; the viewer is the authority
///   on their own preference, so client "tampering" is identical to setting it.
/// - 180-day sliding expiry (re-appended on every consent action), ~50-reveal LRU cap.
/// Malformed cookies parse to defaults — never throw on request input.
/// </summary>
public sealed class AnonPrefs
{
    public const string CookieName = "canalave.prefs";
    public const int MaxRevealedItems = 50;
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(180);

    [JsonPropertyName("m")] public bool Mature { get; set; }

    // Append order = consent order (oldest first) — the LRU trim removes from the front.
    [JsonPropertyName("rs")] public List<int> RevealedStories { get; set; } = [];
    [JsonPropertyName("rg")] public List<int> RevealedGroups { get; set; } = [];
    [JsonPropertyName("rb")] public List<int> RevealedBlogPosts { get; set; } = [];

    public static readonly AnonPrefs Empty = new();

    public bool HasRevealed(RevealedEntityType entityType, int entityId) => entityType switch
    {
        RevealedEntityType.Story => RevealedStories.Contains(entityId),
        RevealedEntityType.Group => RevealedGroups.Contains(entityId),
        RevealedEntityType.BlogPost => RevealedBlogPosts.Contains(entityId),
        _ => false,
    };

    public void AddReveal(RevealedEntityType entityType, int entityId)
    {
        List<int> list = ListFor(entityType);
        list.Remove(entityId); // re-consent moves the item to most-recent
        list.Add(entityId);
        TrimToCap();
    }

    private List<int> ListFor(RevealedEntityType entityType) => entityType switch
    {
        RevealedEntityType.Story => RevealedStories,
        RevealedEntityType.Group => RevealedGroups,
        RevealedEntityType.BlogPost => RevealedBlogPosts,
        _ => throw new ArgumentOutOfRangeException(nameof(entityType), entityType, null),
    };

    private void TrimToCap()
    {
        // Evict least-recently-consented items (front of the longest list) until under the cap.
        while (RevealedStories.Count + RevealedGroups.Count + RevealedBlogPosts.Count > MaxRevealedItems)
        {
            List<int> longest = RevealedStories;
            if (RevealedGroups.Count > longest.Count) longest = RevealedGroups;
            if (RevealedBlogPosts.Count > longest.Count) longest = RevealedBlogPosts;
            longest.RemoveAt(0);
        }
    }

    /// <summary>Parses the cookie from a request; malformed or absent → <see cref="Empty"/>.</summary>
    public static AnonPrefs Read(HttpRequest request)
    {
        if (!request.Cookies.TryGetValue(CookieName, out string? raw) || string.IsNullOrEmpty(raw))
            return Empty;
        try
        {
            return JsonSerializer.Deserialize<AnonPrefs>(raw) ?? Empty;
        }
        catch (JsonException)
        {
            // sanctioned-silent: a malformed preference cookie (hand-edited, truncated) simply
            // means default preferences — request input is never trusted to parse.
            return Empty;
        }
    }

    /// <summary>Writes the cookie to a response with the settled attributes (sliding expiry).</summary>
    public void Append(HttpResponse response)
    {
        response.Cookies.Append(CookieName, JsonSerializer.Serialize(this), new CookieOptions
        {
            SameSite = SameSiteMode.Lax,
            HttpOnly = false,
            Secure = true,
            IsEssential = true,
            Path = "/",
            MaxAge = Lifetime,
        });
    }
}
