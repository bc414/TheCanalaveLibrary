using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Singleton in-process queue of notification ids awaiting an email send — the enqueue half of the
/// write-behind fan-out (<c>layer2-services.md</c> §"Email fan-out").
/// <see cref="ServerNotificationWriteService"/>'s create-core pushes here after its own commit;
/// <see cref="NotificationEmailWorker"/> drains it through <see cref="NotificationEmailFlusher"/>.
///
/// <para><b>Why not send inline:</b> ~22 seeded notification types default
/// <c>EmailEnabled = true</c>, and several of those fan out to every follower of a story or author.
/// An inline send would put N × (connect → auth → send → disconnect) inside a SignalR circuit write
/// path. Decision recorded 2026-07-31, superseding the earlier "build inline first and measure"
/// note — see <c>audit/Notifications.md</c> §"Notification email fan-out."</para>
///
/// <para><b>Ids only, deliberately.</b> The queue stores nothing but the notification id, so
/// eligibility (effective <c>EmailEnabled</c>, <c>EmailConfirmed</c>, still-unread) is resolved at
/// <em>drain</em> time against live rows. A user who opens the notification, or unsubscribes,
/// between enqueue and drain is therefore honoured — which a snapshot payload could not do. It also
/// keeps a site-wide announcement fan-out cheap to enqueue.</para>
///
/// <para><b>Not the coalescing kind of buffer.</b> Unlike <see cref="ReadingProgressBuffer"/> this
/// one is lossless-by-intent and does not merge entries: two notifications for the same user are
/// two emails. The shared shape is the drain/restore contract, not the semantics.</para>
///
/// <para>At N≥2 web nodes each node drains only what it enqueued, which stays correct — every
/// notification is enqueued by exactly the node that created it. See
/// <c>horizontal-scaling.md</c>.</para>
/// </summary>
public sealed class NotificationEmailBuffer
{
    /// <summary>
    /// Hard ceiling on pending ids. Sized well above any plausible single fan-out (the largest is
    /// a site announcement to every user) so it is a runaway backstop, not a routine limit — if it
    /// is ever hit in production, the drain has stalled and that is the actual problem to fix.
    /// </summary>
    public const int MaxDepth = 100_000;

    private readonly ConcurrentQueue<long> _pending = new();

    // Deliberately not IDisposable: instruments on the shared static Meter can't be individually
    // unregistered. Production has exactly one buffer per process; extra registrations from
    // integration-test hosts are benign duplicate observations.
    private readonly ObservableGauge<int> _depthGauge;

    /// <summary>
    /// False when <c>Email:Provider</c> is unset/NoOp. <see cref="Enqueue"/> then discards
    /// silently — with no transport there is nothing to drain, and the worker is not registered
    /// either, so an enabled buffer on such a host would grow without bound forever. This is the
    /// one sanctioned silent drop in this class; every other discard is counted and logged.
    /// </summary>
    public bool IsEnabled { get; }

    public NotificationEmailBuffer(bool isEnabled)
    {
        IsEnabled = isEnabled;
        _depthGauge = CanalaveTelemetry.Email.Meter.CreateObservableGauge(
            "canalave.email.buffer.depth",
            () => _pending.Count,
            unit: "{email}",
            description: "Notification emails currently queued for send.");
    }

    /// <summary>Number of notification ids currently pending send.</summary>
    public int Count => _pending.Count;

    /// <summary>
    /// Queues notifications for email consideration. Called by create-core after its own
    /// <c>SaveChangesAsync</c>, so every id here references a row that actually exists.
    ///
    /// <para>Over-capacity ids are dropped, logged by the caller's counter
    /// (<c>canalave.email.dropped</c>), and reported through the return value — never dropped
    /// silently.</para>
    /// </summary>
    /// <returns>How many ids were discarded because the buffer was at <see cref="MaxDepth"/>.</returns>
    public int Enqueue(IEnumerable<long> notificationIds)
    {
        if (!IsEnabled) return 0;

        int dropped = 0;
        foreach (long id in notificationIds)
        {
            // Approximate check: Count is a snapshot and concurrent enqueues can overshoot it
            // slightly. That is fine — this is a runaway backstop, not an exact quota.
            if (_pending.Count >= MaxDepth)
            {
                dropped++;
                continue;
            }
            _pending.Enqueue(id);
        }

        if (dropped > 0)
            CanalaveTelemetry.Email.Dropped.Add(dropped);

        return dropped;
    }

    /// <summary>
    /// Removes and returns up to <paramref name="maxBatchSize"/> pending ids, oldest first.
    /// Bounding the batch keeps one drain cycle's SMTP conversation to a sane length and keeps a
    /// failure's restore cost proportional.
    /// </summary>
    public List<long> Drain(int maxBatchSize)
    {
        var drained = new List<long>(Math.Min(maxBatchSize, _pending.Count));
        while (drained.Count < maxBatchSize && _pending.TryDequeue(out long id))
            drained.Add(id);
        return drained;
    }

    /// <summary>
    /// Returns a drained batch to the queue after a failed flush so it retries next cycle. Order
    /// is not preserved relative to ids enqueued meanwhile — email ordering within a drain window
    /// carries no meaning, so re-queueing at the tail is correct and lock-free.
    /// </summary>
    public void Restore(IEnumerable<long> batch)
    {
        foreach (long id in batch)
            _pending.Enqueue(id);
    }

    /// <summary>Discards all pending ids. Test isolation only — never called in production.</summary>
    public void Clear()
    {
        while (_pending.TryDequeue(out _)) { }
    }
}
