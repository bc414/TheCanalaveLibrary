using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Singleton in-process queue of <see cref="OutboundEmailWork"/> items awaiting an email send — the
/// enqueue half of the write-behind outbound email pipeline (<c>layer2-services.md</c> §"Outbound email
/// pipeline"). Producers push here after their own commit: notification create-core
/// (<see cref="ServerNotificationWriteService"/>) and Messaging's sends
/// (<see cref="ServerMessagingWriteService"/>). <see cref="OutboundEmailWorker"/> drains it through
/// <see cref="OutboundEmailFlusher"/>, which hands each kind's ids to that kind's
/// <see cref="IOutboundEmailComposer"/>.
///
/// <para><b>Shared infrastructure, no vocabulary.</b> This class was the notification-only
/// <c>ConcurrentQueue&lt;long&gt;</c> under <c>Notifications/</c> until WU-PrivateMessageEmail
/// (2026-10-13) generalized it to <c>(kind, id)</c> items and moved it here: owner ruling D18 lets
/// Messaging share the pipeline only because the pipeline belongs to neither domain.</para>
///
/// <para><b>Why not send inline:</b> 30 of the 47 seeded notification types (as of 2026-10-12) default
/// <c>EmailEnabled = true</c>, and several of those fan out to every follower of a story or author.
/// An inline send would put N × (connect → auth → send → disconnect) inside a SignalR circuit write
/// path. Decision recorded 2026-07-31 — see <c>audit/Notifications.md</c> §"Notification email
/// fan-out."</para>
///
/// <para><b>Ids only, deliberately.</b> The queue stores nothing but the item, so eligibility is
/// resolved at <em>drain</em> time against live rows: a notification opened in-app, a conversation
/// read, or an unsubscribe between enqueue and drain is honoured, which a snapshot payload could not
/// do. For private messages this is load-bearing, not a convenience: "is it still unread?" has to be
/// checked as late as possible (D18). It also keeps a site-wide announcement fan-out cheap to
/// enqueue.</para>
///
/// <para><b>Not the coalescing kind of buffer.</b> Unlike <see cref="ReadingProgressBuffer"/> this
/// one is lossless-by-intent and does not merge entries: two notifications for the same user are
/// two emails (a private-message composer may still fold several message ids of one conversation into
/// one mail; that is its rule, not the buffer's). The shared shape is the drain/restore contract.</para>
///
/// <para>At N≥2 web nodes each node drains only what it enqueued, which stays correct — every item is
/// enqueued by exactly the node that created its row. <c>horizontal-scaling.md</c> §6 has the one
/// private-message caveat.</para>
/// </summary>
public sealed class OutboundEmailBuffer
{
    /// <summary>
    /// Hard ceiling on pending items. Sized well above any plausible single fan-out (the largest is
    /// a site announcement to every user) so it is a runaway backstop, not a routine limit — if it
    /// is ever hit in production, the drain has stalled and that is the actual problem to fix.
    /// </summary>
    public const int MaxDepth = 100_000;

    /// <summary>
    /// How many times one item's mail may be deferred by the SMTP server (a 4xx to its recipient —
    /// <c>IMailTransport.SendBatchAsync</c>'s deferral class) before it is dropped
    /// (WU-NotificationCorrectness review fixes, 2026-10-13). A mailbox that is full for good would
    /// otherwise be retried every cycle for as long as the process lives, and each new item for it
    /// would add another such mail. Ten attempts span about five minutes at the worker's 30-second
    /// cadence (longer under a backlog): enough for a brief per-recipient throttle to clear. Real
    /// greylisting retries belong to the mail provider's relay, which queues for the remote server
    /// itself. The row the mail announced is durable either way.
    /// </summary>
    public const int MaxDeferrals = 10;

    private readonly ConcurrentQueue<OutboundEmailWork> _pending = new();

    // Deferral counts for items currently being retried. Empty in the normal case; an entry lives from
    // an item's first deferral until it is sent, judged ineligible, or dropped at MaxDeferrals.
    private readonly ConcurrentDictionary<OutboundEmailWork, int> _deferrals = new();

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

    public OutboundEmailBuffer(bool isEnabled)
    {
        IsEnabled = isEnabled;
        _depthGauge = CanalaveTelemetry.Email.Meter.CreateObservableGauge(
            "canalave.email.buffer.depth",
            () => _pending.Count,
            unit: "{email}",
            description: "Outbound emails (notification and private-message) currently queued for send.");
    }

    /// <summary>Number of items currently pending send.</summary>
    public int Count => _pending.Count;

    /// <summary>
    /// Queues items for email consideration. Producers call this after their own
    /// <c>SaveChangesAsync</c>, so every id here references a row that actually existed at enqueue.
    ///
    /// <para>Over-capacity items are dropped, counted (<c>canalave.email.dropped</c>), and reported
    /// through the return value for the caller to log — never dropped silently.</para>
    /// </summary>
    /// <returns>How many items were discarded because the buffer was at <see cref="MaxDepth"/>.</returns>
    public int Enqueue(IEnumerable<OutboundEmailWork> items)
    {
        if (!IsEnabled) return 0;

        int dropped = 0;
        foreach (OutboundEmailWork item in items)
        {
            // Approximate check: Count is a snapshot and concurrent enqueues can overshoot it
            // slightly. That is fine — this is a runaway backstop, not an exact quota.
            if (_pending.Count >= MaxDepth)
            {
                dropped++;
                continue;
            }
            _pending.Enqueue(item);
        }

        if (dropped > 0)
            CanalaveTelemetry.Email.Dropped.Add(dropped);

        return dropped;
    }

    /// <summary>
    /// Removes and returns up to <paramref name="maxBatchSize"/> pending items, oldest first.
    /// Bounding the batch keeps one drain cycle's SMTP conversation to a sane length and keeps a
    /// failure's restore cost proportional.
    /// </summary>
    public List<OutboundEmailWork> Drain(int maxBatchSize)
    {
        var drained = new List<OutboundEmailWork>(Math.Min(maxBatchSize, _pending.Count));
        while (drained.Count < maxBatchSize && _pending.TryDequeue(out OutboundEmailWork item))
            drained.Add(item);
        return drained;
    }

    /// <summary>
    /// Returns drained items to the queue after a failed flush so they retry next cycle. Order is not
    /// preserved relative to items enqueued meanwhile — email ordering within a drain window carries
    /// no meaning, so re-queueing at the tail is correct and lock-free.
    /// </summary>
    public void Restore(IEnumerable<OutboundEmailWork> batch)
    {
        foreach (OutboundEmailWork item in batch)
            _pending.Enqueue(item);
    }

    /// <summary>
    /// Requeues items whose mail the SMTP server deferred, counting each one's deferrals. An item
    /// deferred for the <see cref="MaxDeferrals"/>th time is not requeued; it is returned so the
    /// caller can count and log the dropped mail (never a silent drop).
    /// </summary>
    /// <returns>The items that reached <see cref="MaxDeferrals"/> and were dropped.</returns>
    public List<OutboundEmailWork> Defer(IEnumerable<OutboundEmailWork> items)
    {
        List<OutboundEmailWork> exhausted = [];
        foreach (OutboundEmailWork item in items)
        {
            int deferrals = _deferrals.AddOrUpdate(item, 1, static (_, n) => n + 1);
            if (deferrals >= MaxDeferrals)
            {
                _deferrals.TryRemove(item, out _);
                exhausted.Add(item);
            }
            else
            {
                _pending.Enqueue(item);
            }
        }
        return exhausted;
    }

    /// <summary>
    /// Forgets the deferral count of items that left the queue for good (sent, dropped, or judged
    /// ineligible), so a later deferral of an unrelated failure starts from zero and the map does not
    /// grow. A no-op in the normal case, when nothing is being retried.
    /// </summary>
    public void ForgetDeferrals(IEnumerable<OutboundEmailWork> items)
    {
        if (_deferrals.IsEmpty) return;
        foreach (OutboundEmailWork item in items)
            _deferrals.TryRemove(item, out _);
    }

    /// <summary>How many times <paramref name="item"/> has been deferred so far (0 = never).</summary>
    public int DeferralsOf(OutboundEmailWork item) =>
        _deferrals.TryGetValue(item, out int n) ? n : 0;

    /// <summary>Discards all pending items. Test isolation only — never called in production.</summary>
    public void Clear()
    {
        while (_pending.TryDequeue(out _)) { }
        _deferrals.Clear();
    }
}
