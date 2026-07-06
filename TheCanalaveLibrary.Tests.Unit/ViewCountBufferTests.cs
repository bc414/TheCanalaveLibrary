using FluentAssertions;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="ViewCountBuffer"/> (Feature 45 L2 signal buffering) — per-story sum
/// coalescing, drain atomicity, and additive restore-after-failed-flush. Tier: Unit (directly
/// constructed, no host/DB).
/// </summary>
public class ViewCountBufferTests
{
    [Fact]
    public void Record_SumsViews_PerStory()
    {
        var buffer = new ViewCountBuffer();

        buffer.Record(10);
        buffer.Record(10);
        buffer.Record(10);
        buffer.Record(20);

        var drained = buffer.Drain();
        drained.Should().HaveCount(2);
        drained.Should().ContainSingle(e => e.StoryId == 10 && e.Views == 3);
        drained.Should().ContainSingle(e => e.StoryId == 20 && e.Views == 1);
    }

    [Fact]
    public void Drain_EmptiesTheBuffer()
    {
        var buffer = new ViewCountBuffer();
        buffer.Record(10);

        buffer.Drain().Should().ContainSingle();
        buffer.Count.Should().Be(0);
        buffer.Drain().Should().BeEmpty();
    }

    [Fact]
    public void Restore_AddsBackOntoNewerViews()
    {
        var buffer = new ViewCountBuffer();

        // A batch drained for a flush that will fail…
        buffer.Record(10);
        buffer.Record(10);
        var failedBatch = buffer.Drain();

        // …while more views land mid-flush.
        buffer.Record(10);

        buffer.Restore(failedBatch);

        var drained = buffer.Drain();
        drained.Should().ContainSingle(e => e.StoryId == 10 && e.Views == 3,
            "restored views merge additively with views recorded during the failed flush");
    }

    [Fact]
    public void Clear_DiscardsEverything()
    {
        var buffer = new ViewCountBuffer();
        buffer.Record(10);
        buffer.Record(20);

        buffer.Clear();

        buffer.Count.Should().Be(0);
        buffer.Drain().Should().BeEmpty();
    }
}
