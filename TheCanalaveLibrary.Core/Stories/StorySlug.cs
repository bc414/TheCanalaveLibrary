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

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRun();
}
