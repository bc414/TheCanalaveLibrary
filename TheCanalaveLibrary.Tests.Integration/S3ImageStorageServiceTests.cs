using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
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
    // Real encoded fixtures generated with ImageSharp — the sniff + re-encode pipeline
    // (ImageUploadProcessor) rightly rejects fake byte blobs, so fixtures must decode.
    private static readonly byte[] SmallPng = TestImages.Png(4, 4);
    private static readonly byte[] SmallWebp = TestImages.Webp(4, 4);

    private S3ImageStorageService CreateService() =>
        new(S3ImageStorageService.CreateClient(garage.Options), Options.Create(garage.Options),
            // Directly-constructed processor: anonymous viewer (throttle skipped — throttle
            // behavior is WriteThrottleTests' job), pass-through limiter.
            new ImageUploadProcessor(
                new FakeWriteRateLimitService(),
                FakeActiveUserContext.Anonymous(),
                NullLogger<ImageUploadProcessor>.Instance),
            NullLogger<S3ImageStorageService>.Instance);

    [Fact]
    public async Task SaveAsync_PutsTheObject_AndReturnsTheSameStoredPathShapeAsLocal()
    {
        S3ImageStorageService service = CreateService();
        using MemoryStream content = new(SmallPng);

        string relativePath = await service.SaveAsync(content, "image/png", ImageKind.Cover, ownerId: 999);

        // Identical shape to LocalImageStorageService's return — the interchangeability contract.
        relativePath.Should().MatchRegex(@"^/uploads/stories/999/cover-[0-9a-fA-F-]+\.png$");

        using AmazonS3Client client = S3ImageStorageService.CreateClient(garage.Options);
        using GetObjectResponse response = await client.GetObjectAsync(
            garage.Options.BucketName, relativePath["/uploads/".Length..]);
        response.Headers.ContentType.Should().Be("image/png");
        using MemoryStream downloaded = new();
        await response.ResponseStream.CopyToAsync(downloaded);
        // Bytes are re-encoded by the upload pipeline (never stored verbatim) — assert the stored
        // object is a genuine same-dimensions PNG rather than byte-equal to the input.
        downloaded.Position = 0;
        ImageInfo stored = Image.Identify(downloaded);
        stored.Metadata.DecodedImageFormat!.DefaultMimeType.Should().Be("image/png");
        (stored.Width, stored.Height).Should().Be((4, 4));
    }

    [Fact]
    public async Task SaveAsync_BuildsProfileKeysForProfilePictures()
    {
        S3ImageStorageService service = CreateService();
        using MemoryStream content = new(SmallWebp);

        string relativePath = await service.SaveAsync(content, "image/webp", ImageKind.ProfilePicture, ownerId: 7);

        relativePath.Should().MatchRegex(@"^/uploads/users/7/profile-[0-9a-fA-F-]+\.webp$");
    }

    [Fact]
    public async Task SaveAsync_OnASpoofedContentType_StoresTheSniffedFormatInstead()
    {
        S3ImageStorageService service = CreateService();
        // PNG bytes claiming to be JPEG — the classic rename trick. The sniffed format wins:
        // stored key + object content type are png, end to end through a real bucket.
        using MemoryStream content = new(SmallPng);

        string relativePath = await service.SaveAsync(content, "image/jpeg", ImageKind.Cover, ownerId: 998);

        relativePath.Should().MatchRegex(@"^/uploads/stories/998/cover-[0-9a-fA-F-]+\.png$");
        using AmazonS3Client client = S3ImageStorageService.CreateClient(garage.Options);
        using GetObjectResponse response = await client.GetObjectAsync(
            garage.Options.BucketName, relativePath["/uploads/".Length..]);
        response.Headers.ContentType.Should().Be("image/png");
    }

    [Fact]
    public async Task DeleteAsync_RemovesThePreviouslySavedObject()
    {
        S3ImageStorageService service = CreateService();
        using MemoryStream content = new(SmallPng);
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
}
