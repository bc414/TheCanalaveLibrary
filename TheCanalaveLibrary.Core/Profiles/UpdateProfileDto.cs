namespace TheCanalaveLibrary.Core;

/// <summary>
/// Payload for <see cref="IUserSettingsService.UpdateProfileAsync"/>. Covers the Profile
/// settings sub-form: tagline (plain text) and bio (rich HTML from <c>EditorView</c>).
/// The service sanitizes <paramref name="ProfileTextHtml"/> before persisting to
/// <c>UserProfile.Text</c>. <c>null</c> on either field means "leave unchanged."
/// </summary>
public record UpdateProfileDto(string? Tagline, string? ProfileTextHtml);
