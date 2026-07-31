using System.Collections.Concurrent;
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
/// <para><see cref="FailNextBatchWith"/> exists to drive the flusher's restore-on-failure path:
/// that behaviour is the whole reason mail failures can't lose notifications, and it is
/// unobservable without a transport that can be made to fail on demand.</para>
/// </summary>
public sealed class RecordingMailTransport : IMailTransport
{
    private readonly ConcurrentQueue<OutgoingMail> _sent = new();

    /// <summary>Every message handed to the transport, in send order.</summary>
    public IReadOnlyList<OutgoingMail> Sent => [.. _sent];

    /// <summary>
    /// When set, the next <see cref="SendBatchAsync"/> throws this instead of recording, then
    /// clears itself — modelling a connection-level SMTP failure (the case the flusher restores
    /// the batch for), not a per-message rejection.
    /// </summary>
    public Exception? FailNextBatchWith { get; set; }

    public Task SendAsync(OutgoingMail mail, CancellationToken cancellationToken = default) =>
        SendBatchAsync([mail], cancellationToken);

    public Task SendBatchAsync(IReadOnlyList<OutgoingMail> batch, CancellationToken cancellationToken = default)
    {
        if (FailNextBatchWith is Exception failure)
        {
            FailNextBatchWith = null;
            throw failure;
        }

        foreach (OutgoingMail mail in batch)
            _sent.Enqueue(mail);

        return Task.CompletedTask;
    }

    public void Clear()
    {
        while (_sent.TryDequeue(out _)) { }
        FailNextBatchWith = null;
    }
}
