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
/// </summary>
internal static class PersistentStateTestSupport
{
    internal static void AddPersistentComponentState(this BunitContext ctx)
    {
        ctx.Services.AddLogging();
        ctx.Services.AddSingleton<ComponentStatePersistenceManager>();
        ctx.Services.AddSingleton(sp => sp.GetRequiredService<ComponentStatePersistenceManager>().State);
    }
}
