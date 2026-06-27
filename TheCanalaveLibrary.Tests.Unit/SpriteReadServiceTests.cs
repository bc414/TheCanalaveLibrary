using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="ServerSpriteReadService"/> (WU2). The service builds its existence
/// cache at construction time by scanning <see cref="IWebHostEnvironment.WebRootPath"/> — no DB,
/// no host. Each test that needs files creates them first, then constructs the service, so the
/// startup scan picks them up. Tests that check the absence/fallback path construct the service
/// against an empty (or minimally populated) temp directory.
/// </summary>
public class SpriteReadServiceTests : IDisposable
{
    private readonly string _webRoot;

    public SpriteReadServiceTests()
    {
        _webRoot = Path.Combine(Path.GetTempPath(), $"sprite-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_webRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_webRoot))
            Directory.Delete(_webRoot, recursive: true);
    }

    // ── animated path returned when the .webp exists ────────────────────────────

    [Fact]
    public void GetSpriteUrl_WhenAnimatedFileExists_AndUserPrefersAnimated_ReturnsAnimatedPath()
    {
        const string theme = "default";
        const string identifier = "pikachu";
        CreateSpriteFile("sprites", "themes", theme, "animated", $"{identifier}.webp");
        var sut = BuildSut();

        var result = sut.GetSpriteUrl(theme, identifier, userPrefersAnimatedSprites: true);

        result.Should().Be($"/sprites/themes/{theme}/animated/{identifier}.webp");
    }

    // ── static fallback when user doesn't prefer animated ───────────────────────

    [Fact]
    public void GetSpriteUrl_WhenUserDoesNotPreferAnimated_ReturnsStaticPath()
    {
        const string theme = "default";
        const string identifier = "eevee";
        CreateSpriteFile("sprites", "themes", theme, "static", $"{identifier}.png");
        var sut = BuildSut();

        var result = sut.GetSpriteUrl(theme, identifier, userPrefersAnimatedSprites: false);

        result.Should().Be($"/sprites/themes/{theme}/static/{identifier}.png");
    }

    // ── static fallback when user prefers animated but .webp is absent ──────────

    [Fact]
    public void GetSpriteUrl_WhenUserPrefersAnimated_ButAnimatedFileMissing_FallsBackToStaticPng()
    {
        const string theme = "default";
        const string identifier = "snorlax";
        // Only the static PNG exists; no animated .webp.
        CreateSpriteFile("sprites", "themes", theme, "static", $"{identifier}.png");
        var sut = BuildSut();

        var result = sut.GetSpriteUrl(theme, identifier, userPrefersAnimatedSprites: true);

        result.Should().Be($"/sprites/themes/{theme}/static/{identifier}.png",
            "static fallback applies when the .webp is absent, even if the user prefers animated");
    }

    // ── unknown.png fallback when neither file exists ───────────────────────────

    [Fact]
    public void GetSpriteUrl_WhenNeitherAnimatedNorStaticFileExists_ReturnsUnknownPath()
    {
        const string theme = "default";
        const string identifier = "missingno";
        // No files created at all — empty webroot.
        var sut = BuildSut();

        var result = sut.GetSpriteUrl(theme, identifier, userPrefersAnimatedSprites: false);

        result.Should().Be($"/sprites/themes/{theme}/unknown.png",
            "unknown.png is the last-resort fallback for a sprite that doesn't exist in any form");
    }

    [Fact]
    public void GetSpriteUrl_WhenNeitherFileExists_AndUserPrefersAnimated_ReturnsUnknownPath()
    {
        const string theme = "dark";
        const string identifier = "missingno";
        var sut = BuildSut();

        var result = sut.GetSpriteUrl(theme, identifier, userPrefersAnimatedSprites: true);

        result.Should().Be($"/sprites/themes/{theme}/unknown.png");
    }

    // ── theme parameter is honoured ─────────────────────────────────────────────

    [Fact]
    public void GetSpriteUrl_DifferentTheme_UsesCorrectThemePath()
    {
        const string theme = "dark";
        const string identifier = "umbreon";
        CreateSpriteFile("sprites", "themes", theme, "static", $"{identifier}.png");
        var sut = BuildSut();

        var result = sut.GetSpriteUrl(theme, identifier, userPrefersAnimatedSprites: false);

        result.Should().Be($"/sprites/themes/{theme}/static/{identifier}.png");
    }

    // ── helpers ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a new <see cref="ServerSpriteReadService"/> against the test's webroot. Must be
    /// called AFTER all <see cref="CreateSpriteFile"/> calls — the startup scan runs in the
    /// constructor, so files must exist before construction.
    /// </summary>
    private ServerSpriteReadService BuildSut() =>
        new(new FakeWebHostEnvironment(_webRoot));

    private void CreateSpriteFile(params string[] pathSegments)
    {
        var fullPath = Path.Combine([_webRoot, .. pathSegments]);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, []);  // empty file — only existence matters
    }

    // Minimal fake IWebHostEnvironment — only WebRootPath is used by ServerSpriteReadService.
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
