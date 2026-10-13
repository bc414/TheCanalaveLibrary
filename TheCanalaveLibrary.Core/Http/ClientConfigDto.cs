namespace TheCanalaveLibrary.Core;

/// <summary>
/// Deploy-time server settings the WASM client must honor, served by <c>GET /api/client-config</c>
/// (layer5-wasm.md §"Server configuration the client needs"; WU-ParityAndRemaining, 2026-10-13). The
/// server reads each value once into a registered singleton of this record, and both its own consumers
/// and the endpoint read that instance, so the two render phases cannot disagree. Every field carries
/// its built-in default, which is also what the client keeps when the fetch fails.
/// </summary>
/// <param name="SpriteBaseUrl">The <c>Sprites:BaseUrl</c> sprite root
/// (<see cref="OptimisticSpriteReadService"/>).</param>
public sealed record ClientConfigDto(string SpriteBaseUrl = ClientConfigDto.DefaultSpriteBaseUrl)
{
    /// <summary>The wwwroot sprite root, used when <c>Sprites:BaseUrl</c> is unset.</summary>
    public const string DefaultSpriteBaseUrl = "/sprites/themes";
}
