namespace TheCanalaveLibrary.Core;

/// <summary>
/// The include/exclude id-set pair emitted by <c>TagFilter</c> on each selection change.
/// Consumed by <c>ResultsFilterPanel</c> (WU23) which buffers it until the user clicks Apply,
/// then copies the ids into <see cref="StoryFilterDto.IncludedTagIds"/> /
/// <see cref="StoryFilterDto.ExcludedTagIds"/>.
/// </summary>
public sealed record TagFilterSelection(
    IReadOnlyList<int> IncludedTagIds,
    IReadOnlyList<int> ExcludedTagIds)
{
    /// <summary>Empty selection — no included or excluded tags.</summary>
    public static TagFilterSelection Empty { get; } =
        new(Array.Empty<int>(), Array.Empty<int>());
}
