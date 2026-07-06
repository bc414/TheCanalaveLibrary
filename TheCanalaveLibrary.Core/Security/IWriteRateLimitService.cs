namespace TheCanalaveLibrary.Core;

/// <summary>
/// Per-user throttle for abuse-prone writes, enforced inside the L2 write services — the one
/// transport-agnostic point that covers the SignalR circuit today, the L5 HTTP endpoints after
/// the WASM flip, and the residual <c>InteractiveAuto</c> circuit path forever after it. HTTP
/// rate-limiting middleware cannot see circuit traffic, which is why this lives at the service
/// layer and not (only) in the pipeline. See security.md §"Write Throttling".
///
/// Contract: call immediately after the write method's auth guard (the caller is authenticated,
/// so a real userId exists) and before any DB work.
/// </summary>
public interface IWriteRateLimitService
{
    /// <summary>
    /// Consumes one token from the (<paramref name="userId"/>, <paramref name="kind"/>) bucket.
    /// Throws <see cref="WriteRateLimitExceededException"/> when the bucket is empty.
    /// </summary>
    void EnsureAllowed(WriteActionKind kind, int userId);
}
