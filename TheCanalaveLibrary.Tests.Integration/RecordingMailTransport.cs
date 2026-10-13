using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Test double for <see cref="IMailTransport"/> that records every message instead of sending it —
/// the seam that lets the notification-email fan-out be asserted end to end without SMTP.
/// Registered by <see cref="TestAppFactory"/> in place of <c>NoOpMailTransport</c>.
///
/// <para>Singleton on the collection-shared host, so <see cref="Clear"/> is called from
/// <c>IntegrationTestBase</c>'s per-test reset alongside the signal buffers.</para>
///
/// <para><see cref="FailNextBatchWith"/>, <see cref="FailAfter"/> and <see cref="DeferUser"/> exist to
/// drive the flusher's restore-on-failure paths: that behaviour is the whole reason mail failures
/// can't lose notifications, and it is unobservable without a transport that can be made to fail on
/// demand.</para>
/// </summary>
public sealed class RecordingMailTransport : IMailTransport
{
    private readonly ConcurrentQueue<OutgoingMail> _sent = new();
    private readonly ConcurrentDictionary<int, byte> _deferredUsers = new();
    private (int Processed, Exception Inner)? _failAfter;

    /// <summary>Every message handed to the transport, in send order.</summary>
    public IReadOnlyList<OutgoingMail> Sent => [.. _sent];

    /// <summary>
    /// When set, the next <see cref="SendBatchAsync"/> throws this instead of recording, then
    /// clears itself — a transport failure that reports no progress, which the flusher treats as
    /// "nothing was processed" and restores every composed mail for.
    /// </summary>
    public Exception? FailNextBatchWith { get; set; }

    /// <summary>
    /// Makes the next <see cref="SendBatchAsync"/> process its first <paramref name="processed"/>
    /// messages and then throw <c>MailBatchInterruptedException(processed, inner, deferred)</c> — a
    /// mid-batch SMTP death (or a cancellation between messages), the partial-progress half of the
    /// <see cref="IMailTransport.SendBatchAsync"/> contract. A processed message to a
    /// <see cref="DeferUser"/> recipient is reported as deferred, not recorded. Clears itself after
    /// firing.
    /// </summary>
    public void FailAfter(int processed, Exception inner) => _failAfter = (processed, inner);

    /// <summary>
    /// Makes every message to <paramref name="userId"/> come back deferred — a mailbox that keeps
    /// answering <c>RCPT TO</c> with a 4xx (the contract's deferral class) — until
    /// <see cref="AcceptUser"/> or <see cref="Clear"/>. Deferred messages are not recorded.
    /// </summary>
    public void DeferUser(int userId) => _deferredUsers[userId] = 0;

    /// <summary>Stops deferring <paramref name="userId"/>'s messages (the mailbox recovered).</summary>
    public void AcceptUser(int userId) => _deferredUsers.TryRemove(userId, out _);

    /// <summary>
    /// Mirrors <c>SmtpMailTransport.SendAsync</c>: a batch abort surfaces as its inner exception, and
    /// a deferral is dropped (the single-message path has no retry queue).
    /// </summary>
    public async Task SendAsync(OutgoingMail mail, CancellationToken cancellationToken = default)
    {
        try
        {
            await SendBatchAsync([mail], cancellationToken);
        }
        catch (MailBatchInterruptedException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }
    }

    public Task<IReadOnlyList<int>> SendBatchAsync(IReadOnlyList<OutgoingMail> batch, CancellationToken cancellationToken = default)
    {
        if (FailNextBatchWith is Exception failure)
        {
            FailNextBatchWith = null;
            throw failure;
        }

        int limit = batch.Count;
        Exception? abortWith = null;
        if (_failAfter is var (processed, inner))
        {
            _failAfter = null;
            limit = Math.Min(processed, batch.Count);
            abortWith = inner;
        }

        List<int> deferred = [];
        for (int i = 0; i < limit; i++)
        {
            if (_deferredUsers.ContainsKey(batch[i].UserId))
                deferred.Add(i);
            else
                _sent.Enqueue(batch[i]);
        }

        if (abortWith is not null)
            throw new MailBatchInterruptedException(limit, abortWith, deferred);

        return Task.FromResult<IReadOnlyList<int>>(deferred);
    }

    public void Clear()
    {
        while (_sent.TryDequeue(out _)) { }
        _deferredUsers.Clear();
        FailNextBatchWith = null;
        _failAfter = null;
    }
}
