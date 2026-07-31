namespace TheCanalaveLibrary.Core;

/// <summary>
/// Thrown by <c>ServerStoryAcknowledgmentWriteService</c> when a credit request fails input
/// validation (missing ids, self-credit, unknown role, duplicate). Mirrors
/// <see cref="StoryLineageValidationException"/>.
/// </summary>
public class StoryAcknowledgmentValidationException(IReadOnlyList<string> errors)
    : CanalaveValidationException(string.Join(" ", errors), errors);
