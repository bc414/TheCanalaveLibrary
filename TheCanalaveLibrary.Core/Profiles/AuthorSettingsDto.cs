namespace TheCanalaveLibrary.Core;

/// <summary>
/// DTO projection of <see cref="AuthorSettings"/> (JSON complex property on <see cref="User"/>).
/// Used as a sub-record on <see cref="UserSettingsDto"/> (read) and as the payload for
/// <see cref="IUserSettingsService.UpdateAuthorSettingsAsync"/> (write).
/// </summary>
public record AuthorSettingsDto(Rating DefaultStoryRating);
