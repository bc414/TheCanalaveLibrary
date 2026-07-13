using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IThemeReadService"/> (Feature 3 — Sprite &amp; Theme System).
/// Trivial read-only interface, no parameters. Public — mirrors <c>ITagReadService.GetTagDirectoryAsync</c>'s
/// public-lookup treatment: a small, stable, seeded list with no sensitive data.
/// </summary>
public static class ThemeEndpoints
{
    public static WebApplication MapThemeEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/themes");

        group.MapGet("/", async (IThemeReadService themes) =>
            Results.Ok(await themes.GetThemesAsync()));

        return app;
    }
}
