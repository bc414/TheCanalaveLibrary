namespace TheCanalaveLibrary.Core;

/// <summary>
/// DTO projection of <see cref="ReaderSettings"/> (JSON complex property on <see cref="User"/>).
/// Used as a sub-record on <see cref="UserSettingsDto"/> (read) and as the payload for
/// <see cref="IUserSettingsService.UpdateReaderSettingsAsync"/> (write).
/// </summary>
public record ReaderSettingsDto(
    string FontName,
    int FontSize,
    float LineHeight,
    int TextWidth,
    bool JustifyText,
    bool AutoLoadNextChapter,
    bool CollapseCommentThreads,
    int DefaultPaginationSize,
    DefaultSortOrder DefaultSearchSort,
    ReadingBackgroundEnum ReadingBackground = ReadingBackgroundEnum.SiteDefault,
    SavedTagSelectionSortEnum SavedTagSelectionSort = SavedTagSelectionSortEnum.DateCreatedDesc);
