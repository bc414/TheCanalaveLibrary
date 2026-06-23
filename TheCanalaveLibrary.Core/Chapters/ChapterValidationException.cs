namespace TheCanalaveLibrary.Core;

/// <summary>
/// Thrown by <see cref="IChapterWriteService"/> when a chapter or version DTO fails
/// Tier-2/Tier-3 validation. Mirrors <c>StoryValidationException</c>.
/// </summary>
public class ChapterValidationException(List<string> errors) : Exception(string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors.AsReadOnly();
}
