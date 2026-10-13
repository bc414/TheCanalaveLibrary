namespace TheCanalaveLibrary.Server;

/// <summary>
/// Thrown by <see cref="IMailTransport.SendBatchAsync"/> when a batch stops part-way — the
/// partial-progress half of the transport's failure contract (WU-NotificationCorrectness,
/// 2026-10-12; <c>layer2-services.md</c> §"Outbound email pipeline" → "Transport failure contract").
///
/// <para><see cref="ProcessedCount"/> is the number of <b>leading</b> messages the transport finished
/// with: sent, dropped as a per-message failure, or deferred by the server (those are listed in
/// <see cref="DeferredIndices"/> and must be retried like the tail). Messages
/// <c>[ProcessedCount..]</c> were not sent and are safe to retry; the sent and dropped ones before it
/// must not be sent again. The real cause is the <see cref="Exception.InnerException"/> — a connect or
/// auth failure (<c>ProcessedCount = 0</c>), a connection-class send failure, or the
/// <see cref="OperationCanceledException"/> of a cancellation between messages.</para>
///
/// <para><b>One ambiguous message.</b> If the server accepted DATA but its reply was lost, that message
/// is not counted as processed and is retried, so it may be delivered twice. That is accepted
/// (at-least-once for exactly one message); every other message in the batch is exactly-once.</para>
/// </summary>
/// <param name="processedCount">Leading messages the transport finished with.</param>
/// <param name="inner">The exception that stopped the batch.</param>
/// <param name="deferredIndices">Indices below <paramref name="processedCount"/> whose recipient the
/// server deferred (a 4xx to <c>RCPT TO</c>); not sent, so the caller retries them. Null = none.</param>
public sealed class MailBatchInterruptedException(
    int processedCount, Exception inner, IReadOnlyList<int>? deferredIndices = null)
    : Exception($"Mail batch stopped after {processedCount} message(s): {inner.Message}", inner)
{
    /// <summary>Leading messages the transport finished with (sent, dropped, or deferred).</summary>
    public int ProcessedCount { get; } = processedCount;

    /// <summary>
    /// Indices within the processed prefix that the server deferred and that were therefore not sent
    /// (see <see cref="IMailTransport.SendBatchAsync"/>'s deferral class). Empty when none.
    /// </summary>
    public IReadOnlyList<int> DeferredIndices { get; } = deferredIndices ?? [];
}
