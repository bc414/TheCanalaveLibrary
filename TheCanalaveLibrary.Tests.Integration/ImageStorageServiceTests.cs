using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Filesystem round-trip for <c>LocalImageStorageService</c> — no DB involved, but still needs a real
/// host (for <see cref="IWebHostEnvironment"/> + DI), and the host needs a reachable Postgres for
/// Program.cs's startup migration/seed check, hence still using <see cref="PostgresFixture"/>.
/// <see cref="TestAppFactory"/> redirects <c>WebRootPath</c> to a per-factory temp folder so these
/// tests never touch the real <c>wwwroot/uploads/</c>. <c>IImageStorageService</c> is registered
/// Scoped (matching production), so every resolution here goes through an explicit
/// <see cref="IServiceScope"/> rather than the factory's root provider.
/// </summary>
[Collection("Postgres")]
public class ImageStorageServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    // Minimal valid 1x1 PNG — same fixture bytes the WU12 dev-diagnostics endpoint used.
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    private TestAppFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new TestAppFactory(postgres.ConnectionString);
        _ = _factory.Services;
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task SaveAsync_WritesFileUnderWebRoot_AndReturnsAHostRelativePath()
    {
        string relativePath = await SaveOnePixelPngAsync(ownerId: 999);

        relativePath.Should().MatchRegex(@"^/uploads/stories/999/cover-[0-9a-fA-F-]+\.png$");

        string fullPath = ToFullPath(relativePath);
        File.Exists(fullPath).Should().BeTrue();
        (await File.ReadAllBytesAsync(fullPath)).Should().Equal(OnePixelPng);
    }

    [Fact]
    public async Task DeleteAsync_RemovesThePreviouslySavedFile()
    {
        string relativePath = await SaveOnePixelPngAsync(ownerId: 1000);
        string fullPath = ToFullPath(relativePath);
        File.Exists(fullPath).Should().BeTrue("the save in the test's own setup should have succeeded");

        using IServiceScope scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IImageStorageService>().DeleteAsync(relativePath);

        File.Exists(fullPath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_OnAPathTraversalAttempt_DoesNotDeleteOutsideUploads()
    {
        // A sentinel file outside wwwroot/uploads/ that a traversal escape would target.
        string sentinelPath = Path.Combine(_factory.WebRootPath, "sentinel.txt");
        await File.WriteAllTextAsync(sentinelPath, "do not delete me");

        using IServiceScope scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IImageStorageService>().DeleteAsync("/uploads/../sentinel.txt");

        File.Exists(sentinelPath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_RejectsAnUnsupportedContentType()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IImageStorageService imageStorage = scope.ServiceProvider.GetRequiredService<IImageStorageService>();
        using MemoryStream content = new(OnePixelPng);

        Func<Task> act = async () => await imageStorage.SaveAsync(content, "image/gif", ImageKind.Cover, 1);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private string ToFullPath(string relativePath) =>
        Path.Combine(_factory.WebRootPath, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

    private async Task<string> SaveOnePixelPngAsync(int ownerId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IImageStorageService imageStorage = scope.ServiceProvider.GetRequiredService<IImageStorageService>();
        using MemoryStream content = new(OnePixelPng);
        return await imageStorage.SaveAsync(content, "image/png", ImageKind.Cover, ownerId);
    }
}
