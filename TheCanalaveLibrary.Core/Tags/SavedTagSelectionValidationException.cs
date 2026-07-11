namespace TheCanalaveLibrary.Core;

/// <summary>
/// Thrown by <c>ServerSavedTagSelectionWriteService</c> when a create/update/copy operation fails
/// domain validation (nickname/description length, empty tag set, duplicate nickname, invalid copy
/// source). Mirrors <see cref="SeriesValidationException"/>/<see cref="GroupValidationException"/>.
/// </summary>
public class SavedTagSelectionValidationException(IReadOnlyList<string> errors)
    : Exception(string.Join(" ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
