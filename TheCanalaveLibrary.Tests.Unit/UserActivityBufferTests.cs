using FluentAssertions;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="UserActivityBuffer"/> (WU-SiteDailyStat, Feature 62 L2 signal
/// buffering) — per-user latest-timestamp coalescing, drain atomicity, and latest-wins restore
/// after a failed flush. Tier: Unit (directly constructed, no host/DB).
/// </summary>
public class UserActivityBufferTests
{
    [Fact]
    public void Record_KeepsOneEntry_PerUser()
    {
        var buffer = new UserActivityBuffer();

        buffer.Record(10);
        buffer.Record(10);
        buffer.Record(20);

        var drained = buffer.Drain();
        drained.Should().HaveCount(2, "repeated pings for the same user coalesce to one entry");
        drained.Should().Contain(e => e.UserId == 10);
        drained.Should().Contain(e => e.UserId == 20);
    }

    [Fact]
    public void Drain_EmptiesTheBuffer()
    {
        var buffer = new UserActivityBuffer();
        buffer.Record(10);

        buffer.Drain().Should().ContainSingle();
        buffer.Count.Should().Be(0);
        buffer.Drain().Should().BeEmpty();
    }

    [Fact]
    public void Restore_KeepsTheLaterTimestamp()
    {
        var buffer = new UserActivityBuffer();
        DateTime older = DateTime.UtcNow.AddMinutes(-10);
        DateTime newer = DateTime.UtcNow;

        // A newer ping is already pending when an older failed batch retries (crash/retry race).
        buffer.Restore([(10, newer)]);
        buffer.Restore([(10, older)]);

        buffer.Drain().Single().LastActiveUtc.Should().Be(newer,
            "restore keeps the later of the two timestamps — a stale retry must never regress it");
    }

    [Fact]
    public void Clear_DiscardsEverything()
    {
        var buffer = new UserActivityBuffer();
        buffer.Record(10);
        buffer.Record(20);

        buffer.Clear();

        buffer.Count.Should().Be(0);
        buffer.Drain().Should().BeEmpty();
    }
}
