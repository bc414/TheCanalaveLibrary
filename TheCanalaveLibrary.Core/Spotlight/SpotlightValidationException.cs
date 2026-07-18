namespace TheCanalaveLibrary.Core;

/// <summary>
/// Thrown by spotlight grant/redemption operations when validation fails (slot ownership,
/// story eligibility, cooldown, block capacity, grant cap). Mirrors
/// <see cref="RecommendationValidationException"/>.
/// </summary>
public class SpotlightValidationException(List<string> errors)
    : CanalaveValidationException(string.Join("; ", errors), errors)
{
    public SpotlightValidationException(string error) : this([error]) { }
}
