using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IViewCountWriteService"/> — HttpClient wrapper over
/// Server/Stories/ViewCountEndpoints.cs. Write-only interface with no matching read service, so this
/// is a single class with no read/write inheritance split (layer5-wasm.md §"Client Service
/// Implementations" — the same "no base/subclass split" treatment as a read-only interface applies
/// here on the write side). No status→exception translation needed: the endpoint has no auth gate
/// and no typed-exception contract (anonymous viewers count by design — see
/// <see cref="IViewCountWriteService"/>'s doc comment) — the same-origin cookie rides along
/// automatically regardless.
/// </summary>
public sealed class ClientViewCountWriteService(HttpClient http) : IViewCountWriteService
{
    public async Task RecordViewAsync(int storyId) =>
        await http.PostAsync($"api/view-counts/{storyId}", content: null);
}
