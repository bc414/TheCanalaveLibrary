using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Registers a real <see cref="PersistentComponentState"/> in a bUnit context.
///
/// <b>Why this exists.</b> bUnit 2.7.2 ships no persistent-state test double, and
/// <see cref="PersistentComponentState"/> has no public constructor — it is only reachable through
/// <see cref="ComponentStatePersistenceManager"/>, which is how the real host builds it too. Any
/// component that injects it (the layout-chrome components that must use the manual persistence
/// API — see <c>layer5-wasm.md</c> "Components that ALSO render on static-SSR pages") needs this
/// call, or bUnit throws "no registered service of type PersistentComponentState".
///
/// <b>What it does and does not prove.</b> The state starts empty, so <c>TryTakeFromJson</c>
/// returns false and components take their fetch path — which is what these tests want to exercise.
/// It does NOT exercise render-mode inference: bUnit renders with no render mode at all and never
/// runs the persistence pass, so the H10 defect class is invisible here no matter how this is
/// registered. That gap is covered at the wire by <c>StaticSsrPageRenderTests</c> (Integration) and
/// statically by <c>scripts/check-render-modes.ps1</c>. See <c>testing.md</c> "What the three tiers
/// structurally can't see".
///
/// <b>Restoring declarative state</b> (<see cref="RestoreDeclarativeStateAsync{TComponent}"/>,
/// WU-PrivateMessageEmail browser pass, 2026-10-13). A full page load restores every
/// <c>[PersistentState]</c> property from the prerender snapshot, and .NET 10 then hands that snapshot
/// back on the component's first parameter set after initialization (<c>layer3-logic.md</c>
/// §"State Persistence"). Reproducing that needs the real restore: the framework's own supplier and a
/// store holding the snapshot under the key the framework computes.
/// </summary>
internal static class PersistentStateTestSupport
{
    /// <summary>
    /// The component bUnit renders the component under test inside. The framework's state key includes
    /// the parent's type name, so it has to match what the renderer really builds.
    /// </summary>
    private const string BunitParentTypeName = "Bunit.Rendering.ContainerFragment";

    internal static void AddPersistentComponentState(this BunitContext ctx)
    {
        ctx.Services.AddLogging();
        ctx.Services.AddSingleton<ComponentStatePersistenceManager>();
        ctx.Services.AddSingleton(sp => sp.GetRequiredService<ComponentStatePersistenceManager>().State);
    }

    /// <summary>
    /// Performs the restore a full page load performs for <typeparamref name="TComponent"/>'s
    /// <c>[PersistentState]</c> properties, so the next render takes its values from the snapshot,
    /// as the interactive pass does after prerendering. Call it after every other service
    /// registration (it builds the service provider) and before rendering the component.
    /// </summary>
    internal static async Task RestoreDeclarativeStateAsync<TComponent>(
        this BunitContext ctx, params (string PropertyName, object? Value)[] snapshot)
        where TComponent : IComponent
    {
        ctx.AddPersistentComponentState();
        ctx.Services.AddSupplyValueFromPersistentComponentStateProvider();

        var persisted = snapshot.ToDictionary(
            entry => StateKey(typeof(TComponent), entry.PropertyName),
            entry => JsonSerializer.SerializeToUtf8Bytes(entry.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        await ctx.Services.GetRequiredService<ComponentStatePersistenceManager>()
            .RestoreStateAsync(new SnapshotStore(persisted));
    }

    /// <summary>
    /// The framework's key for a property of a component with no <c>@key</c>
    /// (<c>PersistentStateValueProviderKeyResolver</c>): SHA-256 of the SHA-256 of
    /// "{parent type}.{component type}.{property}", Base64-encoded.
    /// </summary>
    private static string StateKey(Type componentType, string propertyName)
    {
        byte[] preKey = SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join(".", BunitParentTypeName, componentType.FullName, propertyName)));
        return Convert.ToBase64String(SHA256.HashData(preKey));
    }

    private sealed class SnapshotStore(IDictionary<string, byte[]> state) : IPersistentComponentStateStore
    {
        public Task<IDictionary<string, byte[]>> GetPersistedStateAsync() => Task.FromResult(state);

        public Task PersistStateAsync(IReadOnlyDictionary<string, byte[]> newState) => Task.CompletedTask;
    }
}
