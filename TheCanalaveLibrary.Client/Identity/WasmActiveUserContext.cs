using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IActiveUserContext"/> (Global Flip). Mirrors
/// <c>ServerActiveUserContext</c>'s claims-only reads, sourcing the principal from the
/// deserializing <see cref="AuthenticationStateProvider"/> that
/// <c>AddAuthenticationStateDeserialization</c> registers — with <c>SerializeAllClaims = true</c>
/// on the server, every claim this interface reads (<see cref="ActiveUserClaimTypes"/> + Identity
/// role claims) crosses into the WASM runtime verbatim.
/// <para>
/// The synchronous <c>GetAwaiter().GetResult()</c> is safe here: the deserialization provider
/// materializes auth state from the persisted payload during host startup, so the task is already
/// completed by the time any component resolves this scoped service. Unlike the server twin, no
/// HttpContext source exists and no middleware-ordering hazard applies — but resolution stays lazy
/// for symmetry and to keep construction free.
/// </para>
/// </summary>
public class WasmActiveUserContext(AuthenticationStateProvider authenticationStateProvider)
    : IActiveUserContext
{
    private ClaimsPrincipal? _principal;

    private ClaimsPrincipal Principal => _principal ??=
        authenticationStateProvider.GetAuthenticationStateAsync().GetAwaiter().GetResult().User;

    public bool IsAuthenticated => Principal.Identity?.IsAuthenticated ?? false;

    public int? UserId =>
        IsAuthenticated && int.TryParse(Principal.FindFirstValue(ClaimTypes.NameIdentifier), out int id)
            ? id
            : null;

    // Anonymous defaults mirror ServerActiveUserContext exactly — the two impls must never drift,
    // or the prerendered (server) and hydrated (WASM) renders of the same page diverge.
    public bool ShowMatureContent =>
        IsAuthenticated
        && bool.TryParse(Principal.FindFirstValue(ActiveUserClaimTypes.ShowMatureContent), out bool mature)
        && mature;

    public string Theme => Principal.FindFirstValue(ActiveUserClaimTypes.Theme) ?? "pokemon";

    public bool PrefersAnimatedSprites =>
        !IsAuthenticated
        || !bool.TryParse(Principal.FindFirstValue(ActiveUserClaimTypes.PrefersAnimatedSprites), out bool animated)
        || animated;

    public bool IsModerator => Principal.IsInRole("Moderator");
    public bool IsAdmin => Principal.IsInRole("Admin");
}
