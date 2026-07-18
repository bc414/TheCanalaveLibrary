namespace TheCanalaveLibrary.Core;

/// <summary>
/// Thrown by <c>ServerStoryArcWriteService</c> when an arc create/edit fails validation
/// (empty/duplicate title, Start &gt; End, overlap with another arc — WU45's service-layer
/// business rules; deliberately not DB constraints). Mirrors <see cref="SeriesValidationException"/>.
/// </summary>
public class StoryArcValidationException(IReadOnlyList<string> errors)
    : CanalaveValidationException(string.Join(" ", errors), errors);
