namespace TheCanalaveLibrary.Core;

/// <summary>
/// Thrown by poll write operations when tier-2 validation or a poll business rule fails
/// (config lock, closed-poll edit, vote-count rule). Mirrors <see cref="BlogPostValidationException"/>.
/// </summary>
public class PollValidationException(List<string> errors) : Exception(string.Join("; ", errors))
{
    public PollValidationException(string error) : this([error]) { }

    public IReadOnlyList<string> Errors { get; } = errors;
}
