using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="OptimisticSpriteReadService"/> (WU38 sprite redesign).
/// The service is a pure string builder with no host/disk dependency — no FakeWebHostEnvironment
/// needed. Tests verify the URL shape from (slug, identifier, animated) triples.
/// </summary>
public class SpriteReadServiceTests
{
    private const string DefaultBase = "/sprites/themes";

    private static OptimisticSpriteReadService BuildSut(string? baseUrl = null) =>
        new(baseUrl ?? DefaultBase);

    // ── animated path when prefersAnimated = true ──────────────────────────────

    [Fact]
    public void GetSpriteUrl_WhenPrefersAnimated_ReturnsAnimatedWebpPath()
    {
        var sut = BuildSut();

        var result = sut.GetSpriteUrl("pokemon", "bulbasaur", prefersAnimated: true);

        result.Should().Be("/sprites/themes/pokemon/animated/bulbasaur.webp");
    }

    // ── static path when prefersAnimated = false ────────────────────────────────

    [Fact]
    public void GetSpriteUrl_WhenDoesNotPreferAnimated_ReturnsStaticPngPath()
    {
        var sut = BuildSut();

        var result = sut.GetSpriteUrl("pokemon", "bulbasaur", prefersAnimated: false);

        result.Should().Be("/sprites/themes/pokemon/static/bulbasaur.png");
    }

    // ── slug is used as path segment, not name ──────────────────────────────────

    [Fact]
    public void GetSpriteUrl_UsesSlugNotDisplayName_InPath()
    {
        var sut = BuildSut();

        // "pokemon" slug, not "Pokémon" display name
        var result = sut.GetSpriteUrl("pokemon", "pikachu", prefersAnimated: false);

        result.Should().Contain("/pokemon/").And.NotContain("Pok");
    }

    // ── different theme slug ────────────────────────────────────────────────────

    [Fact]
    public void GetSpriteUrl_DifferentThemeSlug_UsesCorrectSlugInPath()
    {
        var sut = BuildSut();

        var result = sut.GetSpriteUrl("dark", "umbreon", prefersAnimated: true);

        result.Should().Be("/sprites/themes/dark/animated/umbreon.webp");
    }

    // ── configured base URL is prepended ───────────────────────────────────────

    [Fact]
    public void GetSpriteUrl_CustomBaseUrl_IsPrepended()
    {
        var sut = BuildSut("https://cdn.example.com/sprites/themes");

        var result = sut.GetSpriteUrl("pokemon", "snorlax", prefersAnimated: false);

        result.Should().Be("https://cdn.example.com/sprites/themes/pokemon/static/snorlax.png");
    }
}

/// <summary>
/// Unit tests for <see cref="LocalSpriteAssetProbe"/> (WU38 sprite redesign).
/// Checks File.Exists against a temp wwwroot directory — no DB, no host beyond the fake env.
/// </summary>
public class LocalSpriteAssetProbeTests : IDisposable
{
    private readonly string _webRoot;

    public LocalSpriteAssetProbeTests()
    {
        _webRoot = Path.Combine(Path.GetTempPath(), $"probe-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_webRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_webRoot))
            Directory.Delete(_webRoot, recursive: true);
    }

    [Fact]
    public async Task ExistsAsync_WhenStaticPngExists_ReturnsTrue()
    {
        CreateStaticSprite("pokemon", "bulbasaur");
        var probe = BuildProbe();

        bool exists = await probe.ExistsAsync("pokemon", "bulbasaur");

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WhenStaticPngMissing_ReturnsFalse()
    {
        var probe = BuildProbe();

        bool exists = await probe.ExistsAsync("pokemon", "missingno");

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_ChecksStaticNotAnimated_AnimatedAloneReturnsFalse()
    {
        // Only an animated .webp exists — probe checks .png in static/, so returns false.
        CreateAnimatedSprite("pokemon", "pikachu");
        var probe = BuildProbe();

        bool exists = await probe.ExistsAsync("pokemon", "pikachu");

        exists.Should().BeFalse("probe checks static .png; animated .webp alone is insufficient");
    }

    [Fact]
    public async Task ExistsAsync_WrongTheme_ReturnsFalse()
    {
        CreateStaticSprite("pokemon", "eevee");
        var probe = BuildProbe();

        bool exists = await probe.ExistsAsync("dark", "eevee");

        exists.Should().BeFalse("sprite exists only in 'pokemon' theme, not 'dark'");
    }

    // ── helpers ─────────────────────────────────────────────────────────────────

    private LocalSpriteAssetProbe BuildProbe() =>
        new(new FakeWebHostEnvironment(_webRoot));

    private void CreateStaticSprite(string theme, string identifier)
    {
        string dir = Path.Combine(_webRoot, "sprites", "themes", theme, "static");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, $"{identifier}.png"), []);
    }

    private void CreateAnimatedSprite(string theme, string identifier)
    {
        string dir = Path.Combine(_webRoot, "sprites", "themes", theme, "animated");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, $"{identifier}.webp"), []);
    }

    private sealed class FakeWebHostEnvironment(string webRootPath) : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = webRootPath;
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = webRootPath;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
