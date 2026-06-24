namespace TheCanalaveLibrary.Core;

/// <summary>
/// Input for submitting a new recommendation. <see cref="Text"/> is raw HTML from the editor;
/// the write service sanitizes it before persisting.
/// </summary>
public record RecommendationSubmitDto(int StoryId, string Text);
