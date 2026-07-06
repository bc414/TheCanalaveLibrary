using FluentAssertions;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="ReadingProgressBuffer"/> (Feature 44 L2 signal buffering) —
/// coalescing semantics (max progress + latest timestamp per (user, chapter)), drain atomicity,
/// and restore-after-failed-flush merge rules. Tier: Unit (directly constructed, no host/DB).
/// </summary>
public class ReadingProgressBufferTests
{
    [Fact]
    public void Record_CoalescesToMaxProgress_PerUserChapterKey()
    {
        var buffer = new ReadingProgressBuffer();

        buffer.Record(userId: 1, chapterId: 10, progress: 0.3f);
        buffer.Record(userId: 1, chapterId: 10, progress: 0.7f);
        buffer.Record(userId: 1, chapterId: 10, progress: 0.5f); // lower than high-water — ignored

        var drained = buffer.Drain();
        drained.Should().ContainSingle();
        drained[0].UserId.Should().Be(1);
        drained[0].ChapterId.Should().Be(10);
        drained[0].MaxProgress.Should().Be(0.7f);
    }

    [Fact]
    public void Record_DistinctKeys_StaySeparate()
    {
        var buffer = new ReadingProgressBuffer();

        buffer.Record(1, 10, 0.2f);
        buffer.Record(1, 11, 0.4f); // same user, different chapter
        buffer.Record(2, 10, 0.6f); // different user, same chapter

        buffer.Count.Should().Be(3);
        var drained = buffer.Drain();
        drained.Should().HaveCount(3);
        drained.Should().ContainSingle(e => e.UserId == 1 && e.ChapterId == 10 && e.MaxProgress == 0.2f);
        drained.Should().ContainSingle(e => e.UserId == 1 && e.ChapterId == 11 && e.MaxProgress == 0.4f);
        drained.Should().ContainSingle(e => e.UserId == 2 && e.ChapterId == 10 && e.MaxProgress == 0.6f);
    }

    [Fact]
    public void Record_LaterPing_AdvancesTimestamp_EvenWhenProgressIsLower()
    {
        var buffer = new ReadingProgressBuffer();

        buffer.Record(1, 10, 0.8f);
        DateTime afterFirst = DateTime.UtcNow;
        buffer.Record(1, 10, 0.1f); // re-read from the top: progress keeps high-water, ts advances

        var drained = buffer.Drain();
        drained[0].MaxProgress.Should().Be(0.8f);
        drained[0].LastTimestampUtc.Should().BeOnOrAfter(afterFirst);
    }

    [Fact]
    public void Drain_EmptiesTheBuffer()
    {
        var buffer = new ReadingProgressBuffer();
        buffer.Record(1, 10, 0.5f);

        buffer.Drain().Should().ContainSingle();
        buffer.Count.Should().Be(0);
        buffer.Drain().Should().BeEmpty();
    }

    [Fact]
    public void Restore_MergesWithoutRegressing_ProgressOrTimestamp()
    {
        var buffer = new ReadingProgressBuffer();

        // A batch drained for a flush that will fail…
        buffer.Record(1, 10, 0.6f);
        var failedBatch = buffer.Drain();

        // …while a newer, higher ping lands mid-flush.
        buffer.Record(1, 10, 0.9f);
        DateTime afterNewerPing = DateTime.UtcNow;

        buffer.Restore(failedBatch);

        var drained = buffer.Drain();
        drained.Should().ContainSingle();
        drained[0].MaxProgress.Should().Be(0.9f, "restore must never overwrite a newer high-water mark down");
        drained[0].LastTimestampUtc.Should().BeOnOrBefore(afterNewerPing);
    }

    [Fact]
    public void Restore_IntoEmptyBuffer_ReinstatesTheBatchVerbatim()
    {
        var buffer = new ReadingProgressBuffer();
        buffer.Record(1, 10, 0.4f);
        buffer.Record(2, 20, 0.9f);
        var failedBatch = buffer.Drain();
        buffer.Count.Should().Be(0);

        buffer.Restore(failedBatch);

        var drained = buffer.Drain();
        drained.Should().HaveCount(2);
        drained.Should().ContainSingle(e => e.UserId == 1 && e.ChapterId == 10 && e.MaxProgress == 0.4f);
        drained.Should().ContainSingle(e => e.UserId == 2 && e.ChapterId == 20 && e.MaxProgress == 0.9f);
    }

    [Fact]
    public void Clear_DiscardsEverything()
    {
        var buffer = new ReadingProgressBuffer();
        buffer.Record(1, 10, 0.5f);
        buffer.Record(2, 20, 0.5f);

        buffer.Clear();

        buffer.Count.Should().Be(0);
        buffer.Drain().Should().BeEmpty();
    }
}
