using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="INotificationReadService"/>: HttpClient wrapper over NotificationEndpoints
/// (Server/Notifications/NotificationEndpoints.cs). Same DTOs, same method contracts — only the
/// transport differs (the Layer-5 body-swap). Every method requires the same-origin Identity
/// cookie (sent automatically by WASM's fetch-backed HttpClient); the server's blanket
/// <c>RequireAuthorization()</c> on the whole route group means an unauthenticated call surfaces
/// as an <see cref="HttpRequestException"/> (401) via <c>EnsureSuccessStatusCode</c> rather than
/// the server impl's anonymous-safe zero/empty return — components only ever call this service from
/// behind an <c>&lt;AuthorizeView&gt;</c> gate (see NotificationBell.razor), so that path is unreached
/// in practice.
/// </summary>
public class ClientNotificationReadService(HttpClient http) : INotificationReadService
{
    /// <summary>Exposed to the write subclass — primary-ctor params can't be shared directly.</summary>
    protected HttpClient Http { get; } = http;

    public async Task<int> GetUnreadCountAsync() =>
        await Http.GetFromJsonAsync<int>("api/notifications/unread-count");

    public async Task<int> GetTotalCountAsync() =>
        await Http.GetFromJsonAsync<int>("api/notifications/total-count");

    public async Task<NotificationDto[]> GetNotificationsAsync(
        int page,
        int pageSize,
        NotificationFeedOrder order = NotificationFeedOrder.NewestFirst) =>
        await Http.GetFromJsonAsync<NotificationDto[]>(
            $"api/notifications?page={page}&pageSize={pageSize}&order={(int)order}") ?? [];

    public async Task<NotificationSettingDto[]> GetSettingsAsync() =>
        await Http.GetFromJsonAsync<NotificationSettingDto[]>("api/notifications/settings") ?? [];
}
