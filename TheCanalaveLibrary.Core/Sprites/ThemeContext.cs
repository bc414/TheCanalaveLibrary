namespace TheCanalaveLibrary.Core;

/// <summary>
/// Cascading value supplied by <c>ThemeContextProvider</c> in <c>Routes.razor</c> and consumed by
/// sprite render components. Carries the two scalars needed to call
/// <see cref="ISpriteReadService.GetSpriteUrl"/> without injecting <c>IActiveUserContext</c> in
/// SharedUI (which is server-only and would break WASM render components).
///
/// <para>Values are read from auth claims present in both the prerender and the interactive pass,
/// so the resolved sprite URL is byte-identical across the SSR → InteractiveServer handoff (no
/// flicker). See <c>cross-cutting.md</c> "ThemeContext Cascading Provider."</para>
/// </summary>
/// <param name="Slug">URL-safe theme slug (e.g. <c>"pokemon"</c>) — used as a path segment.</param>
/// <param name="PrefersAnimated">Whether the user prefers animated <c>.webp</c> sprites.</param>
public sealed record ThemeContext(string Slug, bool PrefersAnimated)
{
    /// <summary>
    /// Safe default for anonymous users and for any component that cannot cascade a real context.
    /// Uses the seeded default theme and animated = true (the least-restrictive / most-featured mode).
    /// </summary>
    public static readonly ThemeContext Default = new("pokemon", true);
}
