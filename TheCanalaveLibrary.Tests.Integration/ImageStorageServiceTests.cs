using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
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
public class ImageStorageServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    // Real encoded fixture (see TestImages) — the sniff + re-encode pipeline rejects fake blobs.
    private static readonly byte[] SmallPng = TestImages.Png(4, 4);

    [Fact]
    public async Task SaveAsync_WritesFileUnderWebRoot_AndReturnsAHostRelativePath()
    {
        string relativePath = await SaveSmallPngAsync(ownerId: 999);

        relativePath.Should().MatchRegex(@"^/uploads/stories/999/cover-[0-9a-fA-F-]+\.png$");

        string fullPath = ToFullPath(relativePath);
        File.Exists(fullPath).Should().BeTrue();
        // Bytes are re-encoded by the upload pipeline (never stored verbatim) — assert the stored
        // file is a genuine same-dimensions PNG rather than byte-equal to the input.
        ImageInfo stored = Image.Identify(fullPath);
        stored.Metadata.DecodedImageFormat!.DefaultMimeType.Should().Be("image/png");
        (stored.Width, stored.Height).Should().Be((4, 4));
    }

    [Fact]
    public async Task SaveAsync_OnASpoofedContentType_StoresTheSniffedFormatInstead()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IImageStorageService imageStorage = scope.ServiceProvider.GetRequiredService<IImageStorageService>();
        // PNG bytes claiming to be JPEG — the classic rename trick. The sniffed format wins.
        using MemoryStream content = new(SmallPng);

        string relativePath = await imageStorage.SaveAsync(content, "image/jpeg", ImageKind.Cover, 998);

        relativePath.Should().MatchRegex(@"^/uploads/stories/998/cover-[0-9a-fA-F-]+\.png$");
        Image.Identify(ToFullPath(relativePath)).Metadata.DecodedImageFormat!
            .DefaultMimeType.Should().Be("image/png");
    }

    [Fact]
    public async Task DeleteAsync_RemovesThePreviouslySavedFile()
    {
        string relativePath = await SaveSmallPngAsync(ownerId: 1000);
        string fullPath = ToFullPath(relativePath);
        File.Exists(fullPath).Should().BeTrue("the save in the test's own setup should have succeeded");

        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IImageStorageService>().DeleteAsync(relativePath);

        File.Exists(fullPath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_OnAPathTraversalAttempt_DoesNotDeleteOutsideUploads()
    {
        // A sentinel file outside wwwroot/uploads/ that a traversal escape would target.
        string sentinelPath = Path.Combine(Factory.WebRootPath, "sentinel.txt");
        await File.WriteAllTextAsync(sentinelPath, "do not delete me");

        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IImageStorageService>().DeleteAsync("/uploads/../sentinel.txt");

        File.Exists(sentinelPath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_RejectsAnUnsupportedContentType()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IImageStorageService imageStorage = scope.ServiceProvider.GetRequiredService<IImageStorageService>();
        using MemoryStream content = new(SmallPng);

        Func<Task> act = async () => await imageStorage.SaveAsync(content, "image/gif", ImageKind.Cover, 1);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private string ToFullPath(string relativePath) =>
        Path.Combine(Factory.WebRootPath, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

    private async Task<string> SaveSmallPngAsync(int ownerId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IImageStorageService imageStorage = scope.ServiceProvider.GetRequiredService<IImageStorageService>();
        using MemoryStream content = new(SmallPng);
        return await imageStorage.SaveAsync(content, "image/png", ImageKind.Cover, ownerId);
    }
}
