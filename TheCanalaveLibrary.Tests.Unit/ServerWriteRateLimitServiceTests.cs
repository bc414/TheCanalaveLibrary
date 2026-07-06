using System.Threading.RateLimiting;
using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// The per-user write throttle (security.md §"Write Throttling") exercised directly with a
/// tightened limits table — no host, no DB (testing.md Unit tier). AutoReplenishment is OFF and
/// periods are long so nothing refills mid-test: token counts are fully deterministic.
/// </summary>
public sealed class ServerWriteRateLimitServiceTests
{
    private static ServerWriteRateLimitService BuildSut(int tokenLimit = 3) => new(
        new Dictionary<WriteActionKind, TokenBucketRateLimiterOptions>
        {
            [WriteActionKind.Comment] = TestBucket(tokenLimit),
            [WriteActionKind.Message] = TestBucket(tokenLimit),
            [WriteActionKind.Report] = TestBucket(tokenLimit),
            [WriteActionKind.ContentCreate] = TestBucket(tokenLimit),
            [WriteActionKind.ImageUpload] = TestBucket(tokenLimit),
        });

    private static TokenBucketRateLimiterOptions TestBucket(int tokenLimit) => new()
    {
        TokenLimit = tokenLimit,
        TokensPerPeriod = 1,
        ReplenishmentPeriod = TimeSpan.FromMinutes(10),
        AutoReplenishment = false,
        QueueLimit = 0,
    };

    [Fact]
    public void EnsureAllowed_PermitsTheFullBurst_ThenThrowsWithRetryAfter()
    {
        using ServerWriteRateLimitService sut = BuildSut(tokenLimit: 3);

        for (int i = 0; i < 3; i++)
        {
            sut.Invoking(s => s.EnsureAllowed(WriteActionKind.Comment, userId: 1)).Should().NotThrow();
        }

        WriteRateLimitExceededException ex = sut
            .Invoking(s => s.EnsureAllowed(WriteActionKind.Comment, userId: 1))
            .Should().Throw<WriteRateLimitExceededException>().Which;
        ex.Kind.Should().Be(WriteActionKind.Comment);
        ex.RetryAfter.Should().BePositive();
        ex.Message.Should().Contain("second", "the message is user-ready until WU-ErrorHandling lands");
    }

    [Fact]
    public void EnsureAllowed_IsolatesUsers_OneUsersExhaustionNeverThrottlesAnother()
    {
        using ServerWriteRateLimitService sut = BuildSut(tokenLimit: 2);

        sut.EnsureAllowed(WriteActionKind.Comment, userId: 1);
        sut.EnsureAllowed(WriteActionKind.Comment, userId: 1);
        sut.Invoking(s => s.EnsureAllowed(WriteActionKind.Comment, userId: 1))
            .Should().Throw<WriteRateLimitExceededException>();

        sut.Invoking(s => s.EnsureAllowed(WriteActionKind.Comment, userId: 2)).Should().NotThrow();
    }

    [Fact]
    public void EnsureAllowed_IsolatesKinds_AnExhaustedCommentBucketNeverBlocksUploads()
    {
        using ServerWriteRateLimitService sut = BuildSut(tokenLimit: 1);

        sut.EnsureAllowed(WriteActionKind.Comment, userId: 1);
        sut.Invoking(s => s.EnsureAllowed(WriteActionKind.Comment, userId: 1))
            .Should().Throw<WriteRateLimitExceededException>();

        sut.Invoking(s => s.EnsureAllowed(WriteActionKind.ImageUpload, userId: 1)).Should().NotThrow();
    }

    [Fact]
    public void DefaultLimits_CoverEveryWriteActionKind()
    {
        // The parameterless production ctor must have a bucket for every kind — a new enum
        // member without a limits row would KeyNotFound at runtime on its first throttled call.
        using ServerWriteRateLimitService sut = new();

        foreach (WriteActionKind kind in Enum.GetValues<WriteActionKind>())
        {
            sut.Invoking(s => s.EnsureAllowed(kind, userId: 999)).Should().NotThrow(
                $"the default limits table must include {kind}");
        }
    }
}
