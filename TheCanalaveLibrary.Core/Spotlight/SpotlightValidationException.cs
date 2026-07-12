namespace TheCanalaveLibrary.Core;

/// <summary>
/// Thrown by spotlight grant/redemption operations when validation fails (slot ownership,
/// story eligibility, cooldown, block capacity, grant cap). Mirrors
/// <see cref="RecommendationValidationException"/>.
/// </summary>
public class SpotlightValidationException(List<string> errors) : Exception(string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;

    public SpotlightValidationException(string error) : this([error]) { }
}
