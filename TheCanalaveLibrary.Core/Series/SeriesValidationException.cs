namespace TheCanalaveLibrary.Core;

/// <summary>
/// Thrown by <c>ServerSeriesWriteService</c> when a series create/edit operation fails input
/// validation (name/description length, duplicate name for the same author, etc.).
/// Mirrors <see cref="GroupValidationException"/> / <see cref="BlogPostValidationException"/>.
/// </summary>
public class SeriesValidationException(IReadOnlyList<string> errors)
    : Exception(string.Join(" ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
