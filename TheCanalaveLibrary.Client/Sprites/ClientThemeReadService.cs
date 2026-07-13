using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IThemeReadService"/>: HttpClient wrapper over ThemeEndpoints
/// (Server/Sprites/ThemeEndpoints.cs). Read-only, no matching write service — one client class,
/// no read/write inheritance split (layer5-wasm.md §"Client Service Implementations").
/// </summary>
public class ClientThemeReadService(HttpClient http) : IThemeReadService
{
    private HttpClient Http { get; } = http;

    public async Task<IReadOnlyList<ThemeDto>> GetThemesAsync() =>
        await Http.GetFromJsonAsync<List<ThemeDto>>("api/themes") ?? [];
}
