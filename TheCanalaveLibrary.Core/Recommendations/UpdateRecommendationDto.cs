namespace TheCanalaveLibrary.Core;

/// <summary>
/// Input for editing an existing recommendation. <see cref="Text"/> is raw HTML from the editor;
/// the write service re-sanitizes it before persisting.
/// </summary>
public record UpdateRecommendationDto(int RecommendationId, string Text);
