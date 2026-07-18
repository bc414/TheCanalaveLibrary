namespace TheCanalaveLibrary.Core;

/// <summary>
/// Thrown by <see cref="IWriteRateLimitService.EnsureAllowed"/> when a user's token bucket for a
/// <see cref="WriteActionKind"/> is exhausted. The message is deliberately user-ready — until
/// WU-ErrorHandling designs richer error UX, whatever surface catches this may show
/// <see cref="Exception.Message"/> as-is. Endpoints translate this to <c>429 Too Many Requests</c>
/// with <c>retryAfterSeconds</c> in the <c>ProblemDetails.Extensions</c> body — deliberately NOT a
/// <c>Retry-After</c> response header (EndpointHelpers; security.md §"Write Throttling").
/// </summary>
public sealed class WriteRateLimitExceededException(WriteActionKind kind, TimeSpan retryAfter)
    : Exception(BuildMessage(retryAfter))
{
    public WriteActionKind Kind { get; } = kind;

    /// <summary>How long until the bucket replenishes enough for one more attempt.</summary>
    public TimeSpan RetryAfter { get; } = retryAfter;

    private static string BuildMessage(TimeSpan retryAfter)
    {
        int seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        return $"You're doing that a little too fast — please wait {seconds} second{(seconds == 1 ? "" : "s")} and try again.";
    }
}
