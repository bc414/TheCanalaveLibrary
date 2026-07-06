using System.Threading.RateLimiting;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Singleton token-bucket throttle behind <see cref="IWriteRateLimitService"/> — the library
/// types from <c>System.Threading.RateLimiting</c> used directly (no HTTP middleware involved;
/// this runs inside L2 write services so it covers circuit and HTTP transports alike — see
/// security.md §"Write Throttling" for the placement rationale and the limits table, which this
/// class is the code mirror of). One partition per (userId, kind); idle partitions are cleaned
/// up by <see cref="PartitionedRateLimiter"/> itself.
///
/// Naming/lifetime precedent: <c>ServerHtmlSanitizationService</c> — configured once at
/// construction, thread-safe thereafter.
/// </summary>
public sealed class ServerWriteRateLimitService : IWriteRateLimitService, IDisposable
{
    /// <summary>
    /// security.md's limits table in code. TokenLimit = burst; one token per
    /// ReplenishmentPeriod = sustained rate (smoother than refilling a full window at once).
    /// </summary>
    private static readonly Dictionary<WriteActionKind, TokenBucketRateLimiterOptions> DefaultLimits = new()
    {
        [WriteActionKind.Comment] = BucketOf(tokenLimit: 10, replenishmentSeconds: 6),         // 10 burst, 10/min
        [WriteActionKind.Message] = BucketOf(tokenLimit: 10, replenishmentSeconds: 6),         // 10 burst, 10/min
        [WriteActionKind.Report] = BucketOf(tokenLimit: 5, replenishmentSeconds: 180),         // 5 burst, 1 per 3 min
        [WriteActionKind.ContentCreate] = BucketOf(tokenLimit: 5, replenishmentSeconds: 120),  // 5 burst, 1 per 2 min
        [WriteActionKind.ImageUpload] = BucketOf(tokenLimit: 10, replenishmentSeconds: 30),    // 10 burst, 1 per 30 s
    };

    private readonly IReadOnlyDictionary<WriteActionKind, TokenBucketRateLimiterOptions> _limits;
    private readonly PartitionedRateLimiter<(int UserId, WriteActionKind Kind)> _limiter;

    public ServerWriteRateLimitService() : this(DefaultLimits) { }

    /// <summary>
    /// Test seam (public — the repo deliberately has no InternalsVisibleTo): tightened limits
    /// tables keep throttle tests fast and deterministic. Production always uses the
    /// parameterless ctor via DI.
    /// </summary>
    public ServerWriteRateLimitService(IReadOnlyDictionary<WriteActionKind, TokenBucketRateLimiterOptions> limits)
    {
        _limits = limits;
        _limiter = PartitionedRateLimiter.Create<(int UserId, WriteActionKind Kind), (int UserId, WriteActionKind Kind)>(
            key => RateLimitPartition.GetTokenBucketLimiter(key, k => _limits[k.Kind]));
    }

    public void EnsureAllowed(WriteActionKind kind, int userId)
    {
        using RateLimitLease lease = _limiter.AttemptAcquire((userId, kind));
        if (lease.IsAcquired)
        {
            return;
        }

        TimeSpan retryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan fromLease)
            ? fromLease
            : _limits[kind].ReplenishmentPeriod;
        throw new WriteRateLimitExceededException(kind, retryAfter);
    }

    public void Dispose() => _limiter.Dispose();

    private static TokenBucketRateLimiterOptions BucketOf(int tokenLimit, int replenishmentSeconds) => new()
    {
        TokenLimit = tokenLimit,
        TokensPerPeriod = 1,
        ReplenishmentPeriod = TimeSpan.FromSeconds(replenishmentSeconds),
        AutoReplenishment = true,
        QueueLimit = 0, // reject immediately — a throttled write should fail fast, never stall the circuit
    };
}
