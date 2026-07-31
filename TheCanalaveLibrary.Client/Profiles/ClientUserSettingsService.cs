using System.Net.Http.Headers;
using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IUserSettingsService"/>: HttpClient wrapper over UserSettingsEndpoints
/// (Server/Profiles/UserSettingsEndpoints.cs). Self-referential service (spec's sanctioned CQRS-lite
/// exception) — one client class implementing the whole interface directly, no read/write
/// inheritance split (layer5-wasm.md §"Client Service Implementations" §"Self-referential services").
/// The target user is resolved server-side from the cookie on every call; no <c>userId</c> parameter
/// is ever sent. Auth rides the same-origin Identity cookie — WASM's fetch-backed HttpClient sends it
/// automatically for same-origin requests.
/// </summary>
public sealed class ClientUserSettingsService(HttpClient http) : IUserSettingsService
{
    private HttpClient Http { get; } = http;

    public async Task<UserSettingsDto> GetMySettingsAsync()
    {
        HttpResponseMessage response = await Http.GetAsync("api/user-settings");
        await ThrowIfFailedAsync(response);
        return (await response.Content.ReadFromJsonAsync<UserSettingsDto>())!;
    }

    public async Task UpdateProfileAsync(UpdateProfileDto dto)
    {
        HttpResponseMessage response = await Http.PutAsJsonAsync("api/user-settings/profile", dto);
        await ThrowIfFailedAsync(response);
    }

    public async Task UpdateReaderSettingsAsync(ReaderSettingsDto dto)
    {
        HttpResponseMessage response = await Http.PutAsJsonAsync("api/user-settings/reader", dto);
        await ThrowIfFailedAsync(response);
    }

    public async Task UpdatePrivacySettingsAsync(PrivacySettingsDto dto)
    {
        HttpResponseMessage response = await Http.PutAsJsonAsync("api/user-settings/privacy", dto);
        await ThrowIfFailedAsync(response);
    }

    public async Task UpdateAuthorSettingsAsync(AuthorSettingsDto dto)
    {
        HttpResponseMessage response = await Http.PutAsJsonAsync("api/user-settings/author", dto);
        await ThrowIfFailedAsync(response);
    }

    public async Task UpdateAppearanceAsync(int themeId, bool prefersAnimated)
    {
        HttpResponseMessage response = await Http.PutAsync(
            $"api/user-settings/appearance?themeId={themeId}" +
            $"&prefersAnimated={(prefersAnimated ? "true" : "false")}",
            content: null);
        await ThrowIfFailedAsync(response);
    }

    /// <summary>
    /// Multipart upload (layer5-wasm.md §"Streams and multipart") — builds a
    /// <see cref="MultipartFormDataContent"/> with a <see cref="StreamContent"/> part; the endpoint
    /// reads it back via <c>IFormFile</c>. Mirrors <c>ClientStoryWriteService.UploadCoverArtAsync</c>.
    /// </summary>
    public async Task<string> UploadProfilePictureAsync(Stream content, string contentType)
    {
        using MultipartFormDataContent form = new();
        using StreamContent streamContent = new(content);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(streamContent, "file", "profile-picture");

        HttpResponseMessage response = await Http.PostAsync("api/user-settings/profile-picture", form);
        await ThrowIfFailedAsync(response);
        return (await response.Content.ReadFromJsonAsync<string>())!;
    }

    /// <summary>
    /// Status-code → contract-exception translation, delegated to the shared
    /// <see cref="ClientHttpHelpers.ThrowIfWriteFailedAsync"/> (WU-ErrorHandling2, 2026-07-30 —
    /// previously collapsed 401/403 into one <see cref="InvalidOperationException"/>, predating
    /// <see cref="SessionExpiredException"/>). <c>UpdateAuthorSettingsAsync</c>'s pinned-story
    /// ownership/visibility business rule throws <see cref="UserSettingsValidationException"/>
    /// server-side → 400, reconstructed here carrying <c>ProblemDetails.Detail</c>.
    /// </summary>
    private static Task ThrowIfFailedAsync(HttpResponseMessage response) =>
        ClientHttpHelpers.ThrowIfWriteFailedAsync(
            response, detail => new UserSettingsValidationException([detail]));
}
