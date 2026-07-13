using System.Net;
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

    public async Task UpdateAppearanceAsync(int themeId, bool prefersAnimated, bool prefersDataSaver)
    {
        HttpResponseMessage response = await Http.PutAsync(
            $"api/user-settings/appearance?themeId={themeId}" +
            $"&prefersAnimated={(prefersAnimated ? "true" : "false")}" +
            $"&prefersDataSaver={(prefersDataSaver ? "true" : "false")}",
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
    /// Status-code → contract-exception translation. <see cref="IUserSettingsService"/>'s only
    /// documented exception is <see cref="InvalidOperationException"/> (unauthenticated caller —
    /// see <c>ServerUserSettingsService.RequireCurrentUserId</c>); every service throw site is that
    /// guard, except <c>UpdateAuthorSettingsAsync</c>'s pinned-story business-rule guard, which the
    /// shared server-side <c>EndpointHelpers.ExecuteWriteAsync</c> still maps to 401 uniformly (the
    /// same flagged, out-of-scope mismatch as <c>ClientFollowingWriteService</c>'s analogous cases) —
    /// so 401/403 both translate to <see cref="InvalidOperationException"/> here, carrying the
    /// server's message through rather than losing it.
    /// </summary>
    private static async Task ThrowIfFailedAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                string? detail = await ClientHttpHelpers.ReadProblemDetailAsync(response);
                throw new InvalidOperationException(detail ?? "This operation requires an authenticated user.");
            default:
                response.EnsureSuccessStatusCode(); // throws HttpRequestException with the status
                return;
        }
    }
}
