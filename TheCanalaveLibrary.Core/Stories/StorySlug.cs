using System.Globalization;
using System.Text.RegularExpressions;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Pure slug-text transform, extracted from <c>ServerStoryWriteService</c> so it's unit-testable with
/// no DbContext (uniqueness scanning stays server-side — see
/// <c>ServerStoryWriteService.GenerateUniqueSlugAsync</c>, which calls <see cref="Slugify"/> as its
/// first step). Server-only, never client-editable — see spec §3.7.
/// </summary>
public static partial class StorySlug
{
    public static string Slugify(string title)
    {
        string lowered = title.Trim().ToLowerInvariant();
        return NonAlphanumericRun().Replace(lowered, "-").Trim('-');
    }

    /// <summary>
    /// True when <paramref name="segment"/>, as the third segment of <c>/story/{id}/{segment}</c>, is
    /// an app route rather than a slug: the literal <c>edit</c> (<c>StoryEditorPage</c>) or an integer
    /// (the chapter reading route <c>/story/{id}/{ChapterNumber:int}</c>). Matched the way the router
    /// matches them — the literal case-insensitively, the number with the <c>int</c> constraint's
    /// parse. The one rule both sides of the slug namespace share: the canonical-slug redirect never
    /// treats such a segment as a stale slug, and the slug generator never mints one. Found by the
    /// WU-StoryLifecycle browser pass (2026-09-30): the redirect sent every full load of a published
    /// story's editor to its story page.
    /// </summary>
    public static bool IsReservedRouteSegment(string segment) =>
        segment.Equals("edit", StringComparison.OrdinalIgnoreCase)
        || int.TryParse(segment, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRun();
}
