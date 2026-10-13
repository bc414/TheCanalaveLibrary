using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// The moderation hard delete's cover erasure against a real Garage bucket (owner ruling D14,
/// WU-BlobCleanup): the S3 twin of <see cref="BlobCleanupTests.HardDelete_ErasesTheCover_AndItsUrlThen404s"/>.
/// The integration host always runs the Local provider (Program.cs reads <c>ImageStorage:Provider</c>
/// eagerly), so the moderation service is built over a directly-constructed
/// <see cref="S3ImageStorageService"/> on the same client configuration production uses, and the
/// object's absence is read back with a raw <c>GetObject</c>. Tier: Integration.
/// </summary>
[Collection("Postgres")]
public sealed class BlobCleanupGarageTests(PostgresFixture postgres, GarageFixture garage)
    : IntegrationTestBase(postgres), IClassFixture<GarageFixture>
{
    [Fact]
    public async Task HardDelete_DeletesTheCoverObjectFromTheBucket()
    {
        int authorId = await SeedUserAsync("garage-author");
        int storyId = await SeedStoryAsync(authorId);
        int reporterId = await SeedUserAsync("garage-reporter");
        int modId = await SeedUserAsync("garage-moderator");

        using AmazonS3Client client = S3ImageStorageService.CreateClient(garage.Options);
        using IServiceScope scope = Factory.Services.CreateScope();
        S3ImageStorageService s3 = ActivatorUtilities.CreateInstance<S3ImageStorageService>(
            scope.ServiceProvider, (IAmazonS3)client, Options.Create(garage.Options));

        string cover;
        using (MemoryStream png = new(TestImages.Png(4, 4)))
            cover = await s3.SaveAsync(png, "image/png", ImageKind.Cover, storyId);
        string key = cover[ImageUploadRules.UploadsPrefix.Length..];

        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.StoryListings.Where(l => l.StoryId == storyId)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.CoverArtRelativeUrl, cover));
        short reasonId = await db.ReportReasons.OrderBy(r => r.ReportReasonId).Select(r => r.ReportReasonId).FirstAsync();
        Report report = new()
        {
            ReportedEntityType = ReportedEntityType.Story,
            ReportedEntityId = storyId,
            ReportReasonId = reasonId,
            ReporterUserId = reporterId,
            ReportedUserId = authorId,
            ReportStatusId = ReportStatusEnum.Open,
            DateReported = DateTime.UtcNow,
        };
        db.Reports.Add(report);
        await db.SaveChangesAsync();

        using (GetObjectResponse before = await client.GetObjectAsync(garage.Options.BucketName, key))
            before.HttpStatusCode.Should().Be(HttpStatusCode.OK, "precondition: the object exists");

        SetActiveUser(FakeActiveUserContext.Moderator(modId));
        using (IServiceScope modScope = Factory.Services.CreateScope())
            await ActivatorUtilities.CreateInstance<ServerModerationWriteService>(
                    modScope.ServiceProvider, (IImageStorageService)s3)
                .ResolveWithRemovalAsync(report.ReportId, "Illegal content.", hardDelete: true);

        Func<Task> get = async () => await client.GetObjectAsync(garage.Options.BucketName, key);
        (await get.Should().ThrowAsync<AmazonS3Exception>())
            .Which.StatusCode.Should().Be(HttpStatusCode.NotFound, "the illegal image is gone from the bucket (D14)");
    }
}
