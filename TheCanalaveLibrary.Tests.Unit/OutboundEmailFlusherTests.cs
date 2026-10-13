using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// <see cref="OutboundEmailFlusher"/> constructed directly over stub composers and a stub transport —
/// the domain-free routing and failure rules of the shared outbound email pipeline (owner ruling D18,
/// WU-PrivateMessageEmail 2026-10-13): per-kind routing, one send for every kind, unroutable items
/// dropped, a registration bug refused at construction, and the after-send hook contract (taken mails
/// only, the processed head of an aborted batch, never a restore on its failure, never a cancelled
/// token). The restore rules themselves are Integration-covered end to end
/// (<c>NotificationEmailFanOutTests</c>, <c>PrivateMessageEmailTests</c>). Tier: Unit (no host, no DB).
/// </summary>
public class OutboundEmailFlusherTests
{
    private readonly OutboundEmailBuffer _buffer = new(isEnabled: true);
    private readonly StubTransport _transport = new();
    private readonly FakeLogger<OutboundEmailFlusher> _logger = new();

    private OutboundEmailFlusher NewFlusher(params IOutboundEmailComposer[] composers) =>
        new(composers, _transport, _buffer, _logger);

    private static OutboundEmailWork Notification(long id) => new(OutboundEmailKind.Notification, id);
    private static OutboundEmailWork Message(long id) => new(OutboundEmailKind.PrivateMessage, id);

    [Fact]
    public void TwoComposersForOneKind_AreRefusedAtConstruction()
    {
        Action act = () => NewFlusher(
            new StubComposer(OutboundEmailKind.Notification), new StubComposer(OutboundEmailKind.Notification));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Notification*");
    }

    [Fact]
    public async Task EachKind_GoesToItsComposer_DeDuplicated_AndEveryMailLeavesInOneBatch()
    {
        var notifications = new StubComposer(OutboundEmailKind.Notification);
        var messages = new StubComposer(OutboundEmailKind.PrivateMessage);
        _buffer.Enqueue([Notification(1), Message(10), Message(10), Notification(2)]);

        int taken = await NewFlusher(notifications, messages).FlushAsync();

        notifications.Calls.Should().ContainSingle().Which.Should().Equal(1, 2);
        messages.Calls.Should().ContainSingle().Which.Should().Equal(10);
        _transport.Batches.Should().ContainSingle("one connection per flush, whatever the mix of kinds")
            .Which.Select(m => m.Subject).Should().BeEquivalentTo(["Notification 1", "Notification 2", "PrivateMessage 10"]);
        taken.Should().Be(3);

        // Each hook gets indices into its OWN composed list. The private-message group follows two
        // notification mails in the batch, so a batch position (2) would be out of range for its one
        // mail, the stamp would fail and only log, and the run would be re-mailed every flush.
        notifications.AfterSendCalls.Should().ContainSingle().Which.Should().Equal(0, 1);
        messages.AfterSendCalls.Should().ContainSingle().Which.Should().Equal(0);
    }

    [Fact]
    public async Task ADeferredMailInALaterGroup_IsExcludedFromItsOwnGroupsIndices()
    {
        var notifications = new StubComposer(OutboundEmailKind.Notification);
        var messages = new StubComposer(OutboundEmailKind.PrivateMessage);
        _buffer.Enqueue([Notification(1), Message(10), Message(11)]);
        _transport.DeferSubjects.Add("PrivateMessage 10");

        await NewFlusher(notifications, messages).FlushAsync();

        notifications.AfterSendCalls.Should().ContainSingle().Which.Should().Equal(0);
        messages.AfterSendCalls.Should().ContainSingle().Which.Should().Equal(new[] { 1 },
            "batch position 2 is the second mail of the private-message group, and the deferred first one is not taken");
    }

    [Fact]
    public async Task ItemsOfAKindWithNoComposer_AreDroppedWithAnError_AndTheRestStillSend()
    {
        var notifications = new StubComposer(OutboundEmailKind.Notification);
        _buffer.Enqueue([Message(10), Notification(1)]);

        int taken = await NewFlusher(notifications).FlushAsync();

        taken.Should().Be(1);
        _buffer.Count.Should().Be(0, "an unroutable item can never send; restoring it would only spin");
        _logger.Collector.GetSnapshot().Should().Contain(r =>
            r.Level == LogLevel.Error && r.Message.Contains("no composer is registered"));
    }

    [Fact]
    public async Task AfterSend_GetsTheTakenIndices_NotTheDeferredOnes()
    {
        var messages = new StubComposer(OutboundEmailKind.PrivateMessage);
        _buffer.Enqueue([Message(10), Message(11), Message(12)]);
        _transport.DeferSubjects.Add("PrivateMessage 11");

        await NewFlusher(messages).FlushAsync();

        messages.AfterSendCalls.Should().ContainSingle().Which.Should().BeEquivalentTo([0, 2]);
        _buffer.Drain(10).Should().Equal(new[] { Message(11) }, "the deferred mail goes back, unstamped");
    }

    [Fact]
    public async Task AnAbortedBatch_RunsAfterSendForItsProcessedHead_AndRestoresOnlyTheTail()
    {
        var messages = new StubComposer(OutboundEmailKind.PrivateMessage);
        _buffer.Enqueue([Message(10), Message(11), Message(12)]);
        _transport.FailAfter = (1, new IOException("connection reset"));

        Func<Task> flush = () => NewFlusher(messages).FlushAsync();
        await flush.Should().ThrowAsync<MailBatchInterruptedException>();

        messages.AfterSendCalls.Should().ContainSingle().Which.Should().Equal(0);
        _buffer.Drain(10).Should().BeEquivalentTo([Message(11), Message(12)]);
    }

    [Fact]
    public async Task ACancelledSend_StillRecordsTheHead_WithAnUncancelledToken()
    {
        var messages = new StubComposer(OutboundEmailKind.PrivateMessage);
        _buffer.Enqueue([Message(10), Message(11)]);
        _transport.FailAfter = (1, new OperationCanceledException());

        Func<Task> flush = () => NewFlusher(messages).FlushAsync();
        await flush.Should().ThrowAsync<OperationCanceledException>();

        messages.AfterSendCalls.Should().ContainSingle().Which.Should().Equal(0);
        messages.AfterSendTokens.Should().ContainSingle().Which.CanBeCanceled.Should().BeFalse(
            "the mail has left; a shutdown must not lose the record of it");
    }

    [Fact]
    public async Task AFailingAfterSendHook_IsLogged_AndRestoresNothing()
    {
        var messages = new StubComposer(OutboundEmailKind.PrivateMessage) { AfterSendFailure = new InvalidOperationException("db down") };
        _buffer.Enqueue([Message(10)]);

        int taken = await NewFlusher(messages).FlushAsync();

        taken.Should().Be(1);
        _buffer.Count.Should().Be(0, "restoring a sent mail would send it a second time");
        _logger.Collector.GetSnapshot().Should().Contain(r =>
            r.Level == LogLevel.Error && r.Message.Contains("After-send step"));
    }

    [Fact]
    public async Task AFailingSecondComposer_RestoresTheWholeBatch_AndSendsNothing()
    {
        var notifications = new StubComposer(OutboundEmailKind.Notification);
        var messages = new StubComposer(OutboundEmailKind.PrivateMessage) { ComposeFailure = new TimeoutException() };
        _buffer.Enqueue([Notification(1), Message(10)]);

        Func<Task> flush = () => NewFlusher(notifications, messages).FlushAsync();
        await flush.Should().ThrowAsync<TimeoutException>();

        _transport.Batches.Should().BeEmpty();
        _buffer.Drain(10).Should().BeEquivalentTo([Notification(1), Message(10)]);
        notifications.AfterSendCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task AComposerWithNoHook_IsFine()
    {
        var notifications = new StubComposer(OutboundEmailKind.Notification) { WithHook = false };
        _buffer.Enqueue([Notification(1)]);

        (await NewFlusher(notifications).FlushAsync()).Should().Be(1);
    }

    // ── Stubs ────────────────────────────────────────────────────────────────────

    /// <summary>One mail per id, subject "{Kind} {id}", restore key = the id; records each call.</summary>
    private sealed class StubComposer(OutboundEmailKind kind) : IOutboundEmailComposer
    {
        public OutboundEmailKind Kind => kind;
        public List<IReadOnlyList<long>> Calls { get; } = [];
        public List<IReadOnlyList<int>> AfterSendCalls { get; } = [];
        public List<CancellationToken> AfterSendTokens { get; } = [];
        public Exception? ComposeFailure { get; init; }
        public Exception? AfterSendFailure { get; init; }
        public bool WithHook { get; init; } = true;

        public Task<ComposedEmails> ComposeAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken)
        {
            Calls.Add(ids);
            if (ComposeFailure is not null) throw ComposeFailure;

            List<ComposedEmail> mails = [.. ids.Select(id => new ComposedEmail(id,
                new OutgoingMail($"u{id}@test.invalid", $"{kind} {id}", "<p>body</p>", kind.ToString(), (int)id)))];
            return Task.FromResult(WithHook
                ? new ComposedEmails(mails, (taken, token) =>
                {
                    AfterSendCalls.Add(taken);
                    AfterSendTokens.Add(token);
                    return AfterSendFailure is null ? Task.CompletedTask : Task.FromException(AfterSendFailure);
                })
                : new ComposedEmails(mails));
        }
    }

    /// <summary>Records batches; defers chosen subjects; can abort after n messages like SMTP.</summary>
    private sealed class StubTransport : IMailTransport
    {
        public List<IReadOnlyList<OutgoingMail>> Batches { get; } = [];
        public HashSet<string> DeferSubjects { get; } = [];
        public (int Processed, Exception Inner)? FailAfter { get; set; }

        public Task SendAsync(OutgoingMail mail, CancellationToken cancellationToken = default) =>
            SendBatchAsync([mail], cancellationToken);

        public Task<IReadOnlyList<int>> SendBatchAsync(IReadOnlyList<OutgoingMail> batch, CancellationToken cancellationToken = default)
        {
            Batches.Add(batch);
            List<int> deferred = [.. batch.Select((m, i) => (m, i)).Where(x => DeferSubjects.Contains(x.m.Subject)).Select(x => x.i)];
            if (FailAfter is var (processed, inner))
                throw new MailBatchInterruptedException(processed, inner, [.. deferred.Where(i => i < processed)]);
            return Task.FromResult<IReadOnlyList<int>>(deferred);
        }
    }
}
