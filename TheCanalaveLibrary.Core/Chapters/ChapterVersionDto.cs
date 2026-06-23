namespace TheCanalaveLibrary.Core;

/// <summary>
/// Describes one version (ChapterContent row) of a chapter for the version picker UI.
/// Only versions the current viewer is permitted to see (ShowMatureContent ceiling) are returned.
/// </summary>
public record ChapterVersionDto(
    long ChapterContentId,
    /// <summary>The <c>SortOrder</c> on <c>ChapterContent</c> — stable version identifier in URLs.</summary>
    int VersionOrder,
    string? VersionName,
    Rating Rating,
    int WordCount,
    bool IsPrimary
);
