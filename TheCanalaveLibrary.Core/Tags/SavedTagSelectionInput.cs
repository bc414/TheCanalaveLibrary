namespace TheCanalaveLibrary.Core;

/// <summary>
/// Data required to create or overwrite a <see cref="SavedTagSelection"/> (WU43). <c>UserId</c> is
/// server-stamped from <see cref="IActiveUserContext.UserId"/> on create; absent here — matches
/// <c>CreateSeriesDto</c>'s "no owner field on the input DTO" convention. Validated by
/// <see cref="SavedTagSelectionValidations.CanSave"/>.
/// </summary>
public record SavedTagSelectionInput(
    string Nickname,
    string? Description,
    bool IsPublic,
    IReadOnlyList<int> IncludedTagIds,
    IReadOnlyList<int> ExcludedTagIds);
