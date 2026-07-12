namespace TheCanalaveLibrary.Core;

/// <summary>
/// Thrown by <c>ServerStoryLineageWriteService</c> when a lineage request fails input validation
/// (missing/self-referential ids, unknown type). Mirrors <see cref="SeriesValidationException"/>.
/// </summary>
public class StoryLineageValidationException(IReadOnlyList<string> errors)
    : Exception(string.Join(" ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
