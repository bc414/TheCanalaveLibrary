namespace TheCanalaveLibrary.Server;

/// <summary>
/// Thrown by <see cref="IMailTransport.SendBatchAsync"/> when a batch stops part-way — the
/// partial-progress half of the transport's failure contract (WU-NotificationCorrectness,
/// 2026-09-30; <c>layer2-services.md</c> §"Email fan-out" → "Transport failure contract").
///
/// <para><see cref="ProcessedCount"/> is the number of <b>leading</b> messages that reached a terminal
/// outcome: sent, or dropped as a per-message failure. Messages <c>[ProcessedCount..]</c> were not
/// sent and are safe to retry; the ones before must not be sent again. The real cause is the
/// <see cref="Exception.InnerException"/> — a connect or auth failure (<c>ProcessedCount = 0</c>), a
/// connection-class send failure, or the <see cref="OperationCanceledException"/> of a cancellation
/// between messages.</para>
///
/// <para><b>One ambiguous message.</b> If the server accepted DATA but its reply was lost, that message
/// is not counted as processed and is retried, so it may be delivered twice. That is accepted
/// (at-least-once for exactly one message); every other message in the batch is exactly-once.</para>
/// </summary>
/// <param name="processedCount">Leading messages that reached a terminal outcome.</param>
/// <param name="inner">The exception that stopped the batch.</param>
public sealed class MailBatchInterruptedException(int processedCount, Exception inner)
    : Exception($"Mail batch stopped after {processedCount} message(s): {inner.Message}", inner)
{
    /// <summary>Leading messages that reached a terminal outcome (sent, or dropped per message).</summary>
    public int ProcessedCount { get; } = processedCount;
}
