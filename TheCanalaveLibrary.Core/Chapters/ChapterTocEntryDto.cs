namespace TheCanalaveLibrary.Core;

/// <summary>
/// A single row in a story's table-of-contents dropdown. Includes a flag so the version picker
/// can show an indicator on chapters that have accessible alternate versions.
/// </summary>
public record ChapterTocEntryDto(
    int ChapterNumber,
    string Title,
    int WordCount,
    bool IsPublished,
    /// <summary>
    /// True when the chapter has at least one alternate <c>ChapterContent</c> row whose rating
    /// the current viewer is permitted to see (<c>ShowMatureContent</c> ceiling applied).
    /// </summary>
    bool HasAlternateVersions
);
