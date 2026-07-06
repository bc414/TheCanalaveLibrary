using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Pass-through <see cref="IWriteRateLimitService"/> — the TestAppFactory default, same pattern
/// as <see cref="FakeActiveUserContext"/>. Existing integration tests legitimately hammer write
/// services in loops (comment paging seeds, at-limit fills); the real token buckets would
/// throttle them into false failures. Throttle behavior itself is covered by
/// <c>WriteThrottleTests</c>, which re-registers the real <c>ServerWriteRateLimitService</c>.
/// </summary>
public sealed class FakeWriteRateLimitService : IWriteRateLimitService
{
    public void EnsureAllowed(WriteActionKind kind, int userId)
    {
        // Always allowed.
    }
}
