namespace TheCanalaveLibrary.Core;

/// <summary>
/// Buffered recorder for <see cref="User.LastActiveUtc"/> pings (WU-SiteDailyStat, Feature 62 L2 —
/// the signal-buffering pattern, layer2-services.md "Signal Buffering"). Contract: eventually
/// durable, may lose the last flush interval's ping on a crash, not read-your-own-write.
///
/// Called **only for authenticated users** — anonymous browsing is never stamped, by design (see
/// layer8-data-marts.md §"site_daily_stats" for the privacy reasoning: first-party auth-session
/// data, no tracking cookie, no logged-out fingerprinting).
/// </summary>
public interface IUserActivityWriteService
{
    Task RecordActivityAsync(int userId);
}
