namespace TheCanalaveLibrary.Core;

/// <summary>
/// Describes one version (ChapterContent row) of a chapter for the version picker UI and
/// the edit form. Only versions the current viewer is permitted to see (ShowMatureContent ceiling)
/// are returned by the read service.
/// </summary>
public record ChapterVersionDto(
    long ChapterContentId,
    /// <summary>The <c>SortOrder</c> on <c>ChapterContent</c> — stable version identifier in URLs.</summary>
    int VersionOrder,
    string? VersionName,
    /// <summary>
    /// Raw nullable rating from the DB — null means the version inherits the story's rating.
    /// Use <c>Rating ?? storyRating</c> to compute the effective rating for display.
    /// </summary>
    Rating? Rating,
    int WordCount,
    bool IsPrimary
);
