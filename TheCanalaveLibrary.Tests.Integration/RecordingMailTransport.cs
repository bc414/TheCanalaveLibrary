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
/// <para><see cref="FailNextBatchWith"/> and <see cref="FailAfter"/> exist to drive the flusher's
/// restore-on-failure paths: that behaviour is the whole reason mail failures can't lose
/// notifications, and it is unobservable without a transport that can be made to fail on demand.</para>
/// </summary>
public sealed class RecordingMailTransport : IMailTransport
{
    private readonly ConcurrentQueue<OutgoingMail> _sent = new();
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
    /// Makes the next <see cref="SendBatchAsync"/> record its first <paramref name="processed"/>
    /// messages and then throw <c>MailBatchInterruptedException(processed, inner)</c> — a mid-batch
    /// SMTP death (or a cancellation between messages), the partial-progress half of the
    /// <see cref="IMailTransport.SendBatchAsync"/> contract. Clears itself after firing.
    /// </summary>
    public void FailAfter(int processed, Exception inner) => _failAfter = (processed, inner);

    /// <summary>
    /// Mirrors <c>SmtpMailTransport.SendAsync</c>: a batch abort surfaces as its inner exception.
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

    public Task SendBatchAsync(IReadOnlyList<OutgoingMail> batch, CancellationToken cancellationToken = default)
    {
        if (FailNextBatchWith is Exception failure)
        {
            FailNextBatchWith = null;
            throw failure;
        }

        if (_failAfter is var (processed, inner))
        {
            _failAfter = null;
            int recorded = Math.Min(processed, batch.Count);
            for (int i = 0; i < recorded; i++)
                _sent.Enqueue(batch[i]);
            throw new MailBatchInterruptedException(recorded, inner);
        }

        foreach (OutgoingMail mail in batch)
            _sent.Enqueue(mail);

        return Task.CompletedTask;
    }

    public void Clear()
    {
        while (_sent.TryDequeue(out _)) { }
        FailNextBatchWith = null;
        _failAfter = null;
    }
}
