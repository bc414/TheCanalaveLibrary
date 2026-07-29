using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Minimal in-memory fakes for the Saved Tag Selection services (WU43), shared by
/// <see cref="SavedTagSelectionLoadFlyoutTests"/>, <see cref="SavedTagSelectionSaveDialogTests"/>,
/// and <see cref="TagFilterTests"/>.
/// </summary>

internal sealed class FakeSavedTagSelectionReadService : ISavedTagSelectionReadService
{
    public List<SavedTagSelectionSummaryDto> MySelections { get; set; } = [];
    public Dictionary<int, SavedTagSelectionDetailDto> DetailsById { get; set; } = [];
    public List<SavedTagSelectionDetailDto> PublicSelections { get; set; } = [];

    /// <summary>
    /// Permalink-reachable selections, keyed by id. Deliberately a SEPARATE dictionary from
    /// <see cref="DetailsById"/>: the real service applies two extra gates (IsPublic, the owner's
    /// ProfileVisibility) to this path, so a fake that aliased them would let a component test pass
    /// against a shape the server would refuse.
    /// </summary>
    public Dictionary<int, SavedTagSelectionDetailDto> PermalinkById { get; set; } = [];

    public Task<List<SavedTagSelectionSummaryDto>> GetMySelectionsAsync(SavedTagSelectionSortEnum sort) =>
        Task.FromResult(MySelections);

    public Task<SavedTagSelectionDetailDto?> GetSelectionDetailAsync(int id) =>
        Task.FromResult(DetailsById.GetValueOrDefault(id));

    public Task<List<SavedTagSelectionDetailDto>> GetPublicSelectionsByUserAsync(int userId) =>
        Task.FromResult(PublicSelections);

    public Task<SavedTagSelectionDetailDto?> GetPublicSelectionByIdAsync(int id) =>
        Task.FromResult(PermalinkById.GetValueOrDefault(id));
}

internal sealed class FakeSavedTagSelectionWriteService : ISavedTagSelectionWriteService
{
    public SavedTagSelectionInput? LastCreateInput { get; private set; }
    public (int Id, SavedTagSelectionInput Input)? LastUpdateCall { get; private set; }
    public int? LastDeletedId { get; private set; }
    public int? LastCopiedSourceId { get; private set; }
    public int NextId { get; set; } = 1;

    /// <summary>Throw this on the next CreateAsync call, if set (validation-error test path).</summary>
    public Exception? ThrowOnCreate { get; set; }

    public Task<int> CreateAsync(SavedTagSelectionInput input)
    {
        if (ThrowOnCreate is not null) throw ThrowOnCreate;
        LastCreateInput = input;
        return Task.FromResult(NextId);
    }

    public Task UpdateAsync(int id, SavedTagSelectionInput input)
    {
        LastUpdateCall = (id, input);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(int id)
    {
        LastDeletedId = id;
        return Task.CompletedTask;
    }

    public Task<int> CopyPublicSelectionAsync(int sourceId)
    {
        LastCopiedSourceId = sourceId;
        return Task.FromResult(NextId);
    }
}

internal sealed class FakeUserSettingsService : IUserSettingsService
{
    public SavedTagSelectionSortEnum SavedTagSelectionSort { get; set; } = SavedTagSelectionSortEnum.DateCreatedDesc;

    public Task<UserSettingsDto> GetMySettingsAsync() => Task.FromResult(new UserSettingsDto(
        Tagline: null,
        ProfilePictureRelativeUrl: null,
        ThemeId: 1,
        PrefersAnimatedSprites: true,
        PrefersDataSaverMode: false,
        Reader: new ReaderSettingsDto(
            "Georgia", 16, 1.5f, 800, false, true, 20,
            DefaultSortOrder.DatePublished, ReadingBackgroundEnum.SiteDefault, SavedTagSelectionSort),
        Privacy: new PrivacySettingsDto(
            ProfileVisibility.Public, true, SocialInteractionPermission.Public,
            SocialInteractionPermission.UsersOnly, true, true, false, false),
        Author: new AuthorSettingsDto(Rating.T)));

    public Task UpdateProfileAsync(UpdateProfileDto dto) => Task.CompletedTask;
    public Task UpdateReaderSettingsAsync(ReaderSettingsDto dto) => Task.CompletedTask;
    public Task UpdatePrivacySettingsAsync(PrivacySettingsDto dto) => Task.CompletedTask;
    public Task UpdateAuthorSettingsAsync(AuthorSettingsDto dto) => Task.CompletedTask;
    public Task UpdateAppearanceAsync(int themeId, bool prefersAnimated, bool prefersDataSaver) => Task.CompletedTask;
    public Task<string> UploadProfilePictureAsync(Stream content, string contentType) => Task.FromResult("");
}
