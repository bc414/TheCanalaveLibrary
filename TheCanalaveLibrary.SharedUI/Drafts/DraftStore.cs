using System.Text.Json;
using Microsoft.JSInterop;

namespace TheCanalaveLibrary.SharedUI;

/// <summary>
/// Typed wrapper over draft-autosave.js (localStorage). Scoped; injected by DraftAutosave only —
/// editors talk to DraftAutosave, not this store. Deserialization failures return null (a
/// corrupt/legacy draft is treated as absent, never thrown).
/// </summary>
public sealed class DraftStore(IJSRuntime js)
{
    private static readonly JsonSerializerOptions Options = JsonSerializerOptions.Web;

    /// <summary>Returns false when the device refused the write (private mode, quota).</summary>
    public async Task<bool> SaveAsync(string key, DraftEnvelope draft)
        => await js.InvokeAsync<bool>("canalaveDraft.save", key, JsonSerializer.Serialize(draft, Options));

    public async Task<DraftEnvelope?> LoadAsync(string key)
    {
        string? raw = await js.InvokeAsync<string?>("canalaveDraft.load", key);
        if (string.IsNullOrEmpty(raw)) return null;
        try
        {
            return JsonSerializer.Deserialize<DraftEnvelope>(raw, Options);
        }
        catch (JsonException)
        {
            return null; // corrupt/legacy payload — treat as no draft
        }
    }

    public async Task ClearAsync(string key)
        => await js.InvokeVoidAsync("canalaveDraft.clear", key);
}

/// <summary>
/// One saved draft: when it was captured and the editor's named field values (plain text/HTML —
/// raw user input; sanitization stays where it always was, server-side on submit).
/// </summary>
public sealed record DraftEnvelope(DateTime SavedAtUtc, Dictionary<string, string?> Fields);
