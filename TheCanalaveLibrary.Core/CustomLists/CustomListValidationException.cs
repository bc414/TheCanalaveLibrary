namespace TheCanalaveLibrary.Core;

/// <summary>
/// Thrown by <c>ServerCustomListWriteService</c> when a create/rename/clone operation fails domain
/// validation (name empty/too long, duplicate name, list cap reached, invalid clone source).
/// Mirrors <see cref="SavedTagSelectionValidationException"/>.
/// </summary>
public class CustomListValidationException(IReadOnlyList<string> errors)
    : CanalaveValidationException(string.Join(" ", errors), errors);
