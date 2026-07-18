namespace TheCanalaveLibrary.Core;

/// <summary>
/// Thrown by <see cref="ITagWriteService"/> when a tag operation fails domain validation —
/// e.g. duplicate name within type, parent depth violation, or a delete blocked by references.
/// </summary>
public sealed class TagValidationException(string message)
    : CanalaveValidationException(message, [message]);
