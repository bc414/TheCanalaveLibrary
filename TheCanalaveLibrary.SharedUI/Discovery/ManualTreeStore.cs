using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace TheCanalaveLibrary.SharedUI;

/// <summary>
/// Typed wrapper over manual-tree-search.js (Feature 33 / WU40) — the thin JS module that owns
/// per-frame gestures (pan/zoom, floating-panel drag) and localStorage tree persistence, keeping
/// them off the SignalR circuit (layer3.5-structure.md §"The shared tree canvas"). Scoped;
/// injected by ExploreTab/DeepDiveTab only — same pattern as <see cref="DraftStore"/>.
/// </summary>
public sealed class ManualTreeStore(IJSRuntime js)
{
    /// <summary>localStorage key for one curated tree: one document per (mode, root entity).</summary>
    public static string TreeKey(string mode, bool rootIsStory, int rootEntityId) =>
        $"canalave.tree.{mode}.{(rootIsStory ? "story" : "user")}.{rootEntityId}";

    /// <summary>Returns false when the device refused the write (private mode, quota).</summary>
    public async Task<bool> SaveTreeAsync(string key, string json) =>
        await js.InvokeAsync<bool>("canalaveManualTree.saveTree", key, json);

    public async Task<string?> LoadTreeAsync(string key) =>
        await js.InvokeAsync<string?>("canalaveManualTree.loadTree", key);

    public async Task ClearTreeAsync(string key) =>
        await js.InvokeVoidAsync("canalaveManualTree.clearTree", key);

    public async Task AttachPanAsync(ElementReference wrap, ElementReference canvas, double startX, double startY) =>
        await js.InvokeVoidAsync("canalaveManualTree.attachPan", wrap, canvas, startX, startY);

    public async Task ZoomAsync(ElementReference canvas, double delta) =>
        await js.InvokeVoidAsync("canalaveManualTree.zoom", canvas, delta);

    public async Task ResetPanAsync(ElementReference canvas, double x, double y) =>
        await js.InvokeVoidAsync("canalaveManualTree.resetPan", canvas, x, y);

    public async Task AttachPanelDragAsync(ElementReference panel, ElementReference handle) =>
        await js.InvokeVoidAsync("canalaveManualTree.attachPanelDrag", panel, handle);
}
