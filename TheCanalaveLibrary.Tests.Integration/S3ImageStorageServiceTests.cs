using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Round-trips <c>S3ImageStorageService</c> against a real single-node Garage container
/// (<see cref="GarageFixture"/>) — the service is directly constructed (no web host, no DB), but
/// the S3 endpoint is real, and the client comes from the same
/// <see cref="S3ImageStorageService.CreateClient"/> the production DI path uses, so the
/// R2-interchangeability wire format (unchunked uploads, WHEN_REQUIRED checksums, path-style) is
/// exercised on every request. The <c>/uploads/{**key}</c> serving endpoint is browser-verified
/// under the Aspire AppHost instead (Program.cs reads ImageStorage:Provider eagerly, before
/// WebApplicationFactory config overrides apply — same quirk as the connection string, see
/// TestAppFactory).
/// </summary>
public class S3ImageStorageServiceTests(GarageFixture garage) : IClassFixture<GarageFixture>
{
    // Minimal valid 1x1 PNG — same fixture bytes as ImageStorageServiceTests (the Local impl's suite).
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    private S3ImageStorageService CreateService() =>
        new(S3ImageStorageService.CreateClient(garage.Options), Options.Create(garage.Options),
            NullLogger<S3ImageStorageService>.Instance);

    [Fact]
    public async Task SaveAsync_PutsTheObject_AndReturnsTheSameStoredPathShapeAsLocal()
    {
        S3ImageStorageService service = CreateService();
        using MemoryStream content = new(OnePixelPng);

        string relativePath = await service.SaveAsync(content, "image/png", ImageKind.Cover, ownerId: 999);

        // Identical shape to LocalImageStorageService's return — the interchangeability contract.
        relativePath.Should().MatchRegex(@"^/uploads/stories/999/cover-[0-9a-fA-F-]+\.png$");

        using AmazonS3Client client = S3ImageStorageService.CreateClient(garage.Options);
        using GetObjectResponse response = await client.GetObjectAsync(
            garage.Options.BucketName, relativePath["/uploads/".Length..]);
        response.Headers.ContentType.Should().Be("image/png");
        using MemoryStream downloaded = new();
        await response.ResponseStream.CopyToAsync(downloaded);
        downloaded.ToArray().Should().Equal(OnePixelPng);
    }

    [Fact]
    public async Task SaveAsync_BuildsProfileKeysForProfilePictures()
    {
        S3ImageStorageService service = CreateService();
        using MemoryStream content = new(OnePixelPng);

        string relativePath = await service.SaveAsync(content, "image/webp", ImageKind.ProfilePicture, ownerId: 7);

        relativePath.Should().MatchRegex(@"^/uploads/users/7/profile-[0-9a-fA-F-]+\.webp$");
    }

    [Fact]
    public async Task DeleteAsync_RemovesThePreviouslySavedObject()
    {
        S3ImageStorageService service = CreateService();
        using MemoryStream content = new(OnePixelPng);
        string relativePath = await service.SaveAsync(content, "image/png", ImageKind.Cover, ownerId: 1000);

        await service.DeleteAsync(relativePath);

        using AmazonS3Client client = S3ImageStorageService.CreateClient(garage.Options);
        Func<Task> get = async () => await client.GetObjectAsync(
            garage.Options.BucketName, relativePath["/uploads/".Length..]);
        (await get.Should().ThrowAsync<AmazonS3Exception>())
            .Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_OnANonexistentKey_NoOps()
    {
        S3ImageStorageService service = CreateService();

        Func<Task> act = async () =>
            await service.DeleteAsync("/uploads/stories/424242/cover-00000000-0000-0000-0000-000000000000.png");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteAsync_OnATraversalOrForeignPath_NoOps()
    {
        S3ImageStorageService service = CreateService();

        // Neither shape is a value this service family ever returned — both must no-op
        // without reaching the bucket (TryExtractKey returns null).
        await service.DeleteAsync("/uploads/../secrets.txt");
        await service.DeleteAsync("/somewhere/else.png");
    }

    [Fact]
    public async Task SaveAsync_RejectsAnUnsupportedContentType()
    {
        S3ImageStorageService service = CreateService();
        using MemoryStream content = new(OnePixelPng);

        Func<Task> act = async () => await service.SaveAsync(content, "image/gif", ImageKind.Cover, 1);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SaveAsync_RejectsAPayloadOverTheSizeCap()
    {
        S3ImageStorageService service = CreateService();
        // 1 byte over the 10 MB cap. The S3 impl buffers with limit enforcement, so this holds
        // even for non-seekable browser streams (which the Local impl's CanSeek check can't cover).
        using MemoryStream content = new(new byte[ImageUploadRules.MaxBytes + 1]);

        Func<Task> act = async () => await service.SaveAsync(content, "image/png", ImageKind.Cover, 1);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
