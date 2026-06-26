namespace TheCanalaveLibrary.Core;

/// <summary>
/// The include/exclude id-set pair (and include-mode flag) emitted by <c>TagFilter</c> on each
/// selection change. Consumed by <c>ResultsFilterPanel</c> (WU23/WU28) which buffers it until the
/// user clicks Apply, then copies values into <see cref="StoryFilterDto.IncludedTagIds"/> /
/// <see cref="StoryFilterDto.ExcludedTagIds"/> / <see cref="StoryFilterDto.IncludeMode"/>.
/// </summary>
public sealed record TagFilterSelection(
    IReadOnlyList<int> IncludedTagIds,
    IReadOnlyList<int> ExcludedTagIds,
    TagIncludeMode IncludeMode = TagIncludeMode.And)
{
    /// <summary>Empty selection — no included or excluded tags, AND mode (default).</summary>
    public static TagFilterSelection Empty { get; } =
        new(Array.Empty<int>(), Array.Empty<int>());
}
