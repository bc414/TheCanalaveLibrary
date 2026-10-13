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
/// TestAppFactory). <c>EnumerateAsync</c> (owner ruling D14, WU-BlobCleanup) is covered here too: the
/// stored-path shape and last-modified time, the upload-prefix scope, and continuation paging.
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

    // ── EnumerateAsync (owner ruling D14, WU-BlobCleanup) — the bucket is shared across this class,
    //    so these assert "contains" / "does not contain", never an exact listing. ──────────────────

    private static async Task<List<StoredImage>> EnumerateAllAsync(S3ImageStorageService service)
    {
        List<StoredImage> items = [];
        await foreach (StoredImage item in service.EnumerateAsync()) items.Add(item);
        return items;
    }

    [Fact]
    public async Task EnumerateAsync_ListsSavedCoversAndAvatars_WithTheStoresLastModifiedTime()
    {
        S3ImageStorageService service = CreateService();
        using MemoryStream png = new(SmallPng);
        string cover = await service.SaveAsync(png, "image/png", ImageKind.Cover, ownerId: 501);
        using MemoryStream webp = new(SmallWebp);
        string avatar = await service.SaveAsync(webp, "image/webp", ImageKind.ProfilePicture, ownerId: 502);

        List<StoredImage> items = await EnumerateAllAsync(service);

        foreach (string path in new[] { cover, avatar })
        {
            StoredImage item = items.Should().ContainSingle(i => i.RelativePath == path,
                "a stored path enumerates in exactly the shape SaveAsync returned").Which;
            item.LastModifiedUtc.Kind.Should().Be(DateTimeKind.Utc);
            item.LastModifiedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
        }
    }

    [Fact]
    public async Task EnumerateAsync_NeverListsOutsideTheUploadPrefixes()
    {
        // Sprites may share the production bucket: the sweep must never see them.
        using AmazonS3Client client = S3ImageStorageService.CreateClient(garage.Options);
        string spriteKey = $"sprites/themes/pokemon/static/{Guid.NewGuid()}.png";
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = garage.Options.BucketName,
            Key = spriteKey,
            InputStream = new MemoryStream(SmallPng),
            UseChunkEncoding = false,
        });

        List<StoredImage> items = await EnumerateAllAsync(CreateService());

        items.Should().NotContain(i => i.RelativePath.Contains(spriteKey));
        items.Should().OnlyContain(i => i.RelativePath.StartsWith("/uploads/stories/")
                                        || i.RelativePath.StartsWith("/uploads/users/"));
    }

    [Fact]
    public async Task EnumerateAsync_FollowsTheContinuationToken_AcrossPages()
    {
        S3ImageStorageService saver = CreateService();
        List<string> saved = [];
        for (int i = 0; i < 3; i++)
        {
            using MemoryStream png = new(SmallPng);
            saved.Add(await saver.SaveAsync(png, "image/png", ImageKind.Cover, ownerId: 600 + i));
        }

        // One key per page: three or more pages, so every item past the first needs a continuation token.
        S3ImageStorageOptions onePerPage = new()
        {
            ServiceUrl = garage.Options.ServiceUrl,
            Region = garage.Options.Region,
            AccessKey = garage.Options.AccessKey,
            SecretKey = garage.Options.SecretKey,
            BucketName = garage.Options.BucketName,
            ListPageSize = 1,
        };
        S3ImageStorageService paged = new(S3ImageStorageService.CreateClient(onePerPage), Options.Create(onePerPage),
            new ImageUploadProcessor(new FakeWriteRateLimitService(), FakeActiveUserContext.Anonymous(),
                NullLogger<ImageUploadProcessor>.Instance),
            NullLogger<S3ImageStorageService>.Instance);

        List<StoredImage> items = await EnumerateAllAsync(paged);

        items.Select(i => i.RelativePath).Should().Contain(saved);
        items.Select(i => i.RelativePath).Should().OnlyHaveUniqueItems("paging must not repeat a key");
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
