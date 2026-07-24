namespace TheCanalaveLibrary.SharedUI;

/// <summary>
/// Route → friendly copy for the sign-in-required experience (WU-AccessGate Phase 1).
/// One table, one place: page-level <c>[Authorize]</c> means the ROUTER renders the denial, so
/// per-page copy cannot live in the pages themselves — that is exactly why the old inline
/// <c>&lt;NotAuthorized&gt;</c> blocks on five editor pages were unreachable dead code (deleted in
/// the same work-unit). Matching is ordered: first predicate wins; falls through to
/// <see cref="Generic"/>. Descriptions say what the page is FOR — the denial doubles as an
/// advertisement for the feature, not a dead end.
/// </summary>
public static class SignInRequiredCopy
{
    public sealed record Entry(string Title, string Description, bool IsModeratorArea = false);

    public static readonly Entry Generic = new(
        "Sign in to continue",
        "This page is available to signed-in members.");

    private static readonly (Func<string, bool> Matches, Entry Copy)[] Map =
    [
        (p => p.StartsWith("/mod/", StringComparison.OrdinalIgnoreCase) || p.Equals("/mod", StringComparison.OrdinalIgnoreCase),
            new Entry("Moderation tools", "This area is for site moderators.", IsModeratorArea: true)),
        (p => p.StartsWith("/bookshelves", StringComparison.OrdinalIgnoreCase),
            new Entry("Your bookshelves",
                "The stories you're reading, following, favoriting, and saving for later live here.")),
        (p => p.StartsWith("/my-lists", StringComparison.OrdinalIgnoreCase),
            new Entry("Your lists", "Create, organize, and share custom story collections.")),
        (p => p.StartsWith("/story-lineages", StringComparison.OrdinalIgnoreCase),
            new Entry("Story lineage", "Manage prequel, sequel, and inspired-by links between your stories.")),
        (p => p.StartsWith("/notifications", StringComparison.OrdinalIgnoreCase),
            new Entry("Your notifications", "Updates about your stories, your follows, and replies to you.")),
        (p => p.StartsWith("/messages", StringComparison.OrdinalIgnoreCase),
            new Entry("Your messages", "Private conversations with other members.")),
        (p => p.StartsWith("/settings", StringComparison.OrdinalIgnoreCase) ||
              p.StartsWith("/Account/Manage", StringComparison.OrdinalIgnoreCase),
            new Entry("Your settings", "Reading, privacy, and account preferences.")),
        (p => p.StartsWith("/spotlight", StringComparison.OrdinalIgnoreCase),
            new Entry("Community Spotlight", "Redeem spotlight slots you've been granted for your stories.")),
        // Editors: /story/new, /story/{id}/edit, /story/{id}/chapter/..., /blog/new|edit,
        // /group/new|edit|blog/new, /series/new|edit. Matched by shape, not enumeration.
        (p => (p.StartsWith("/story/", StringComparison.OrdinalIgnoreCase) ||
               p.StartsWith("/blog", StringComparison.OrdinalIgnoreCase) ||
               p.StartsWith("/group", StringComparison.OrdinalIgnoreCase) ||
               p.StartsWith("/series", StringComparison.OrdinalIgnoreCase)) &&
              (p.EndsWith("/new", StringComparison.OrdinalIgnoreCase) ||
               p.EndsWith("/edit", StringComparison.OrdinalIgnoreCase) ||
               p.Contains("/chapter/", StringComparison.OrdinalIgnoreCase)),
            new Entry("Writing tools", "Create and edit stories, chapters, series, blog posts, and groups.")),
        (p => p.Equals("/series", StringComparison.OrdinalIgnoreCase),
            new Entry("Your series", "Group your stories into ordered series.")),
    ];

    /// <summary>Looks up copy for a base-relative path (with or without a leading slash).</summary>
    public static Entry For(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return Generic;
        string path = relativePath.StartsWith('/') ? relativePath : "/" + relativePath;
        // Strip query/fragment before matching.
        int cut = path.IndexOfAny(['?', '#']);
        if (cut >= 0) path = path[..cut];

        foreach ((Func<string, bool> matches, Entry copy) in Map)
            if (matches(path))
                return copy;
        return Generic;
    }
}
