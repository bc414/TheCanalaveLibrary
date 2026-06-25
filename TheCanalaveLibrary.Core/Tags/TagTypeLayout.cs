namespace TheCanalaveLibrary.Core;

/// <summary>
/// Single source of truth for which <see cref="TagTypeEnum"/>s are "unbounded" (Character,
/// Relationship, CrossoverFandom) and therefore rendered with collapsibility + a type jump-nav
/// on the Tag Directory page. Bounded types (Setting, Genre, ContentWarning) render expanded/flat.
/// Mirrors the role <c>BookshelfTabVisuals</c> plays for the bookshelf layout decisions.
/// </summary>
public static class TagTypeLayout
{
    private static readonly HashSet<TagTypeEnum> UnboundedTypes =
    [
        TagTypeEnum.Character,
        TagTypeEnum.Relationship,
        TagTypeEnum.CrossoverFandom,
    ];

    /// <summary>Returns <c>true</c> for Character, Relationship, and CrossoverFandom.</summary>
    public static bool IsUnbounded(TagTypeEnum type) => UnboundedTypes.Contains(type);

    /// <summary>Display label for the type section header.</summary>
    public static string Label(TagTypeEnum type) => type switch
    {
        TagTypeEnum.Character => "Characters",
        TagTypeEnum.Setting => "Settings",
        TagTypeEnum.Genre => "Genres",
        TagTypeEnum.ContentWarning => "Content Warnings",
        TagTypeEnum.CrossoverFandom => "Crossover Fandoms",
        TagTypeEnum.Relationship => "Relationships",
        _ => type.ToString()
    };
}
