using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IUserActivityWriteService"/> — HttpClient wrapper over
/// Server/Identity/UserActivityEndpoints.cs. The interface's <c>userId</c> parameter is accepted here
/// only to satisfy the contract signature; it is deliberately never sent over the wire — the endpoint
/// resolves the caller's own id server-side from the auth cookie via <c>IActiveUserContext</c>
/// (layer5-wasm.md §"Client Service Implementations" "Self-referential services" trust decision,
/// mirroring <c>ClientUserSettingsService</c>). Sending no body/route value at all is what keeps a WASM
/// caller from being able to record activity for an arbitrary other user.
/// <para>
/// No status→exception translation: this is a best-effort buffered ping (layer5-wasm.md §"Buffered-
/// signal ping endpoints"), not a durable write with a typed contract exception — the same-origin
/// Identity cookie rides along automatically on the WASM fetch-backed <see cref="HttpClient"/>.
/// </para>
/// </summary>
public sealed class ClientUserActivityWriteService(HttpClient http) : IUserActivityWriteService
{
    public async Task RecordActivityAsync(int userId) =>
        await http.PostAsync("api/user-activity", content: null);
}
