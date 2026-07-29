using System.Text.Json;
using Microsoft.JSInterop;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.SharedUI;

/// <summary>
/// Typed wrapper over discovery-filter.js (localStorage) — device-local restore of the last applied
/// <c>/discover</c> filter (decision row 13). Scoped; injected by <c>SearchPage</c> only — the same
/// thin-JS-seam pattern as <see cref="DraftStore"/> and <see cref="ManualTreeStore"/>, so the write
/// never touches the circuit and a corrupt/legacy payload reads as absent rather than throwing.
/// <para>
/// The persisted shape and its prune rules live on <see cref="DiscoveryFilterSnapshot"/> (Core, so
/// they are Unit-testable without a JS runtime). <c>[PersistentState]</c> cannot serve this purpose
/// — it only bridges the prerender→interactive handoff (<c>error-handling.md</c>).
/// </para>
/// </summary>
public sealed class DiscoveryFilterStore(IJSRuntime js)
{
    private static readonly JsonSerializerOptions Options = JsonSerializerOptions.Web;

    /// <summary>
    /// One key per viewer per browser. Anonymous viewers get their own bucket so a shared device
    /// never hands one account's filter to the next visitor.
    /// </summary>
    public static string FilterKey(int? userId) =>
        $"canalave.discover.filter.{(userId?.ToString() ?? "anon")}";

    /// <summary>Returns false when the device refused the write (private mode, quota).</summary>
    public async Task<bool> SaveAsync(string key, DiscoveryFilterSnapshot snapshot) =>
        await js.InvokeAsync<bool>("canalaveDiscoveryFilter.save", key, JsonSerializer.Serialize(snapshot, Options));

    public async Task<DiscoveryFilterSnapshot?> LoadAsync(string key)
    {
        string? raw = await js.InvokeAsync<string?>("canalaveDiscoveryFilter.load", key);
        if (string.IsNullOrEmpty(raw)) return null;
        try
        {
            return JsonSerializer.Deserialize<DiscoveryFilterSnapshot>(raw, Options);
        }
        catch (JsonException)
        {
            return null; // corrupt/legacy payload — treat as no saved filter
        }
    }

    public async Task ClearAsync(string key) =>
        await js.InvokeVoidAsync("canalaveDiscoveryFilter.clear", key);
}
