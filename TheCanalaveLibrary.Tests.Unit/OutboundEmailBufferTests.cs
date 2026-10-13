using FluentAssertions;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// <see cref="OutboundEmailBuffer"/> constructed directly: the shared <c>(kind, id)</c> queue of the
/// outbound email pipeline (owner ruling D18, WU-PrivateMessageEmail 2026-10-13, which generalized the
/// notification-only <c>long</c> queue). Pins the drain/restore/defer contract the flusher relies on,
/// and that two kinds with the same id stay two distinct items. Tier: Unit (no host, no DB).
/// </summary>
public class OutboundEmailBufferTests
{
    private static OutboundEmailWork Notification(long id) => new(OutboundEmailKind.Notification, id);
    private static OutboundEmailWork Message(long id) => new(OutboundEmailKind.PrivateMessage, id);

    [Fact]
    public void Drain_IsFirstInFirstOut_AndRespectsTheBatchSize()
    {
        var buffer = new OutboundEmailBuffer(isEnabled: true);
        buffer.Enqueue([Notification(1), Message(2), Notification(3)]);

        buffer.Drain(2).Should().Equal(Notification(1), Message(2));
        buffer.Count.Should().Be(1);
        buffer.Drain(10).Should().Equal(Notification(3));
        buffer.Drain(10).Should().BeEmpty();
    }

    [Fact]
    public void Restore_PutsADrainedBatchBack()
    {
        var buffer = new OutboundEmailBuffer(isEnabled: true);
        buffer.Enqueue([Notification(1), Message(1)]);

        List<OutboundEmailWork> batch = buffer.Drain(10);
        buffer.Count.Should().Be(0);
        buffer.Restore(batch);

        buffer.Drain(10).Should().BeEquivalentTo([Notification(1), Message(1)]);
    }

    [Fact]
    public void TheSameIdUnderTwoKinds_IsTwoItems()
    {
        // A notification id and a message id are unrelated numbers that can collide; the kind is
        // part of the item's identity, for queueing and for deferral counts alike.
        var buffer = new OutboundEmailBuffer(isEnabled: true);
        buffer.Enqueue([Notification(42), Message(42)]);

        buffer.Count.Should().Be(2);
        buffer.Defer([Message(42)]);
        buffer.DeferralsOf(Message(42)).Should().Be(1);
        buffer.DeferralsOf(Notification(42)).Should().Be(0);
    }

    [Fact]
    public void Enqueue_AtMaxDepth_DropsTheOverflow_AndReportsHowMany()
    {
        var buffer = new OutboundEmailBuffer(isEnabled: true);
        buffer.Enqueue(Enumerable.Range(0, OutboundEmailBuffer.MaxDepth).Select(i => Notification(i)));

        int dropped = buffer.Enqueue([Message(1), Message(2)]);

        dropped.Should().Be(2, "a full buffer never drops silently — the caller logs this number");
        buffer.Count.Should().Be(OutboundEmailBuffer.MaxDepth);
    }

    [Fact]
    public void ADisabledBuffer_DiscardsAtEnqueue()
    {
        var buffer = new OutboundEmailBuffer(isEnabled: false);

        buffer.Enqueue([Notification(1), Message(2)]).Should().Be(0);

        buffer.Count.Should().Be(0, "a NoOp host has no drain, so nothing may accumulate");
    }

    [Fact]
    public void Defer_RequeuesUntilMaxDeferrals_ThenReturnsTheItemAsDropped()
    {
        var buffer = new OutboundEmailBuffer(isEnabled: true);

        for (int attempt = 1; attempt < OutboundEmailBuffer.MaxDeferrals; attempt++)
        {
            buffer.Defer([Message(7)]).Should().BeEmpty();
            buffer.Drain(10).Should().Equal(Message(7));
        }

        buffer.Defer([Message(7)]).Should().Equal(Message(7));
        buffer.Count.Should().Be(0);
        buffer.DeferralsOf(Message(7)).Should().Be(0, "a dropped item leaves no deferral state behind");
    }

    [Fact]
    public void ForgetDeferrals_ResetsTheCount()
    {
        var buffer = new OutboundEmailBuffer(isEnabled: true);
        buffer.Defer([Notification(5)]);

        buffer.ForgetDeferrals([Notification(5)]);

        buffer.DeferralsOf(Notification(5)).Should().Be(0);
    }

    [Fact]
    public void Clear_EmptiesTheQueueAndTheDeferralCounts()
    {
        var buffer = new OutboundEmailBuffer(isEnabled: true);
        buffer.Enqueue([Notification(1), Message(2)]);
        buffer.Defer([Message(3)]);

        buffer.Clear();

        buffer.Count.Should().Be(0);
        buffer.DeferralsOf(Message(3)).Should().Be(0);
    }
}
