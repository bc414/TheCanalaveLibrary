namespace TheCanalaveLibrary.Core;

/// <summary>
/// DTO projection of the Author settings sub-form. Carries the <see cref="AuthorSettings"/> JSON
/// group (<see cref="DefaultStoryRating"/>) plus <see cref="PinnedStoryId"/>, which is a hot
/// scalar FK column on <see cref="User"/> — NOT part of the JSON blob (it needs a real FK with
/// ON DELETE SET NULL and joinability in the manual-tree-search pivot; Feature 33 / WU40) — but
/// is edited through the same sub-form, so it rides this DTO (same UI-grouping-≠-storage-grouping
/// pattern as <c>ShowMatureContent</c> on <see cref="PrivacySettingsDto"/>).
/// Used as a sub-record on <see cref="UserSettingsDto"/> (read) and as the payload for
/// <see cref="IUserSettingsService.UpdateAuthorSettingsAsync"/> (write).
/// </summary>
public record AuthorSettingsDto(Rating DefaultStoryRating, int? PinnedStoryId = null);
