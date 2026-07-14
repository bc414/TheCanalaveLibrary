namespace TheCanalaveLibrary.Core;

/// <summary>
/// View-time sort options for the stories in a custom list (settled 2026-07-13: user-selectable
/// sort, no manual ordering — the entity deliberately has no <c>SortOrder</c> column). Not
/// DB-stored — a transient query parameter, mirroring <see cref="SavedTagSelectionSortEnum"/>.
/// </summary>
public enum CustomListSortEnum
{
    DateAddedDesc = 0,   // default — newest-added first
    DateAddedAsc = 1,
    TitleAsc = 2,
    TitleDesc = 3,
}
