namespace TheCanalaveLibrary.Core;

/// <summary>
/// Sort order for <see cref="ISavedTagSelectionReadService.GetMySelectionsAsync"/> (WU43). Default is
/// <see cref="DateCreatedDesc"/>; the effective default is overridable per-user via
/// <c>ReaderSettings.SavedTagSelectionSort</c> (<c>Core/Identity/User.cs</c>).
/// </summary>
public enum SavedTagSelectionSortEnum : short
{
    DateCreatedDesc = 0,
    DateCreatedAsc = 1,
    NicknameAsc = 2,
    NicknameDesc = 3,
}
