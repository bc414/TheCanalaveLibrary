using System.Threading.RateLimiting;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// The service-layer write throttle exercised through the REAL production wiring — every other
/// integration test runs against the pass-through <see cref="FakeWriteRateLimitService"/>
/// (TestAppFactory default); this class re-registers the real
/// <see cref="ServerWriteRateLimitService"/> with a tightened, non-replenishing limits table and
/// proves the L2 write services actually consult it. See security.md §"Write Throttling".
/// </summary>
[Collection("Postgres")]
public sealed class WriteThrottleTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private const int TightLimit = 3;

    private WebApplicationFactory<Program> _throttledFactory = null!;
    private int _posterId;
    private int _wallOwnerId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _posterId = await SeedUserAsync("ThrottlePoster");
        _wallOwnerId = await SeedUserAsync("ThrottleWallOwner");

        _throttledFactory = Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IWriteRateLimitService>();
            services.AddSingleton<IWriteRateLimitService>(new ServerWriteRateLimitService(
                Enum.GetValues<WriteActionKind>().ToDictionary(
                    kind => kind,
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = TightLimit,
                        TokensPerPeriod = 1,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(10),
                        AutoReplenishment = false, // nothing refills mid-test — deterministic counts
                        QueueLimit = 0,
                    })));
        }));
    }

    public override Task DisposeAsync()
    {
        _throttledFactory.Dispose();
        return base.DisposeAsync();
    }

    /// <summary>The derived factory has its own DI container — set ITS fake, not the base one.</summary>
    private void SetThrottledActiveUser(int userId)
    {
        FakeActiveUserContext fake = _throttledFactory.Services.GetRequiredService<FakeActiveUserContext>();
        fake.UserId = userId;
        fake.IsAuthenticated = true;
    }

    [Fact]
    public async Task CommentPosts_ThrottleAtTheBucketLimit_ThroughTheRealServiceWiring()
    {
        SetThrottledActiveUser(_posterId);
        using IServiceScope scope = _throttledFactory.Services.CreateScope();
        ICommentWriteService comments = scope.ServiceProvider.GetRequiredService<ICommentWriteService>();

        for (int i = 0; i < TightLimit; i++)
        {
            await comments.PostUserProfileCommentAsync(
                new PostUserProfileCommentDto(_wallOwnerId, null, $"throttle test comment {i}"));
        }

        Func<Task> overLimit = () => comments.PostUserProfileCommentAsync(
            new PostUserProfileCommentDto(_wallOwnerId, null, "one past the bucket"));

        (await overLimit.Should().ThrowAsync<WriteRateLimitExceededException>())
            .Which.Kind.Should().Be(WriteActionKind.Comment);
    }

    [Fact]
    public async Task ImageUploads_ThrottleAtTheBucketLimit_ThroughTheStorageService()
    {
        SetThrottledActiveUser(_posterId);
        using IServiceScope scope = _throttledFactory.Services.CreateScope();
        IImageStorageService imageStorage = scope.ServiceProvider.GetRequiredService<IImageStorageService>();
        byte[] png = TestImages.Png(4, 4);

        for (int i = 0; i < TightLimit; i++)
        {
            using MemoryStream content = new(png);
            await imageStorage.SaveAsync(content, "image/png", ImageKind.ProfilePicture, _posterId);
        }

        using MemoryStream overLimitContent = new(png);
        Func<Task> overLimit = () =>
            imageStorage.SaveAsync(overLimitContent, "image/png", ImageKind.ProfilePicture, _posterId);

        (await overLimit.Should().ThrowAsync<WriteRateLimitExceededException>())
            .Which.Kind.Should().Be(WriteActionKind.ImageUpload);
    }

    [Fact]
    public async Task AnExhaustedBucket_NeverAffectsADifferentUser()
    {
        SetThrottledActiveUser(_posterId);
        using (IServiceScope scope = _throttledFactory.Services.CreateScope())
        {
            ICommentWriteService comments = scope.ServiceProvider.GetRequiredService<ICommentWriteService>();
            for (int i = 0; i < TightLimit; i++)
            {
                await comments.PostUserProfileCommentAsync(
                    new PostUserProfileCommentDto(_wallOwnerId, null, $"exhaust {i}"));
            }
        }

        // The wall owner comments back — a different user's bucket, untouched.
        SetThrottledActiveUser(_wallOwnerId);
        using IServiceScope replyScope = _throttledFactory.Services.CreateScope();
        ICommentWriteService replyService = replyScope.ServiceProvider.GetRequiredService<ICommentWriteService>();

        Func<Task> reply = () => replyService.PostUserProfileCommentAsync(
            new PostUserProfileCommentDto(_posterId, null, "different user, fresh bucket"));

        await reply.Should().NotThrowAsync();
    }
}
