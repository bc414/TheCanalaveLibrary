using System.Runtime.ExceptionServices;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// The drain half of the write-behind outbound email pipeline (<c>layer2-services.md</c> §"Outbound
/// email pipeline"): takes a bounded batch of <see cref="OutboundEmailWork"/> items off
/// <see cref="OutboundEmailBuffer"/>, hands each kind's ids to that kind's
/// <see cref="IOutboundEmailComposer"/>, and sends every composed mail to <see cref="IMailTransport"/>
/// in <b>one</b> batch over one connection. Body/worker split follows the
/// <c>ReadingProgressFlusher</c>/<c>ReadingProgressFlushWorker</c> pattern, so Integration tests flush
/// deterministically instead of racing a timer.
///
/// <para><b>Domain-free by design</b> (owner ruling D18; WU-PrivateMessageEmail, 2026-10-13, which split
/// the notification-only flusher into this class and <see cref="NotificationEmailComposer"/>). Who is
/// eligible and what the mail says belong to the composers; this class owns only the send and what
/// happens when it fails. A composer is registered per kind; two for one kind is a startup error.</para>
///
/// <para><b>Failure posture (restore rules from WU-NotificationCorrectness, 2026-10-12, unchanged in
/// substance).</b> <see cref="FlushAsync"/> restores to the buffer exactly the items whose mail was not
/// sent, then rethrows; the worker logs and carries on. Per-message failures are handled inside the
/// transport (logged, counted, dropped — see <see cref="IMailTransport.SendBatchAsync"/>). Each composed
/// mail carries a restore key (<see cref="ComposedEmail.RestoreId"/>). Which items go back:</para>
/// <list type="bullet">
///   <item>composing failed (any composer) → the whole drained batch (nothing was sent);</item>
///   <item><see cref="MailBatchInterruptedException"/> → only the restore keys of the composed mails
///   from its <see cref="MailBatchInterruptedException.ProcessedCount"/> on — the leading ones were sent
///   (or dropped as a per-message failure) and must not go twice, and items judged ineligible are
///   already correctly gone;</item>
///   <item>any other transport exception → every composed restore key (assume nothing was
///   processed);</item>
///   <item>a mail the server <b>deferred</b> (a 4xx to its recipient, reported by the transport
///   whether or not the batch then stopped) → requeued through <see cref="OutboundEmailBuffer.Defer"/>,
///   which drops it, counted and logged here, at its <see cref="OutboundEmailBuffer.MaxDeferrals"/>th
///   deferral. It never holds back the rest of the batch;</item>
///   <item>cancellation (a shutdown mid-flight) restores by the same rules and rethrows the
///   <see cref="OperationCanceledException"/> itself, so the caller's shutdown path still sees one;</item>
///   <item>items of a kind with no registered composer are dropped with an Error (a wiring bug —
///   restoring them would only spin).</item>
/// </list>
/// <para><b>After-send hooks</b> (<see cref="ComposedEmails.AfterSendAsync"/>) run for the mails the
/// transport took — after a successful batch and for the processed head of an aborted one — with
/// <see cref="CancellationToken.None"/>; a hook failure is logged at Error and restores nothing (the
/// mail has left). Nothing here ever touches a producer's rows: mail is a side channel, and a mail
/// outage must not corrupt a notification feed or a conversation.</para>
/// </summary>
public sealed class OutboundEmailFlusher
{
    /// <summary>
    /// Maximum items drained in one cycle. Bounds both the SMTP conversation length and the cost of
    /// restoring the batch after a connection failure.
    /// </summary>
    public const int MaxBatchSize = 200;

    private readonly IReadOnlyDictionary<OutboundEmailKind, IOutboundEmailComposer> _composers;
    private readonly IMailTransport _transport;
    private readonly OutboundEmailBuffer _buffer;
    private readonly ILogger<OutboundEmailFlusher> _logger;

    public OutboundEmailFlusher(
        IEnumerable<IOutboundEmailComposer> composers,
        IMailTransport transport,
        OutboundEmailBuffer buffer,
        ILogger<OutboundEmailFlusher> logger)
    {
        var byKind = new Dictionary<OutboundEmailKind, IOutboundEmailComposer>();
        foreach (IOutboundEmailComposer composer in composers)
        {
            if (!byKind.TryAdd(composer.Kind, composer))
            {
                throw new InvalidOperationException(
                    $"Two outbound email composers are registered for kind {composer.Kind} " +
                    $"({byKind[composer.Kind].GetType().Name} and {composer.GetType().Name}).");
            }
        }

        _composers = byKind;
        _transport = transport;
        _buffer = buffer;
        _logger = logger;
    }

    /// <summary>
    /// Drains and sends one batch. Returns the number of emails the transport took (sent, or dropped
    /// as a per-message failure — not the number delivered, see the class doc's failure posture);
    /// mails the server deferred are not counted. On failure the unsent items are already back in the
    /// buffer when this throws.
    /// </summary>
    public async Task<int> FlushAsync(CancellationToken cancellationToken = default) =>
        (await FlushBatchAsync(cancellationToken)).Taken;

    /// <summary>One batch's result: mails the transport took, and whether any drained item left the
    /// queue for good (sent, dropped, or ineligible) rather than going straight back as a deferral.</summary>
    private readonly record struct BatchResult(int Taken, bool Settled);

    /// <summary>One composed mail across every kind: its restore item, the mail, and which composer's
    /// output (and which index in it) it came from — the after-send hooks need the latter.</summary>
    private readonly record struct Outgoing(OutboundEmailWork Restore, OutgoingMail Mail, int Group, int IndexInGroup);

    /// <summary>One composer's output for this batch, with the kind it was composed for.</summary>
    private readonly record struct ComposedGroup(OutboundEmailKind Kind, ComposedEmails Emails);

    private async Task<BatchResult> FlushBatchAsync(CancellationToken cancellationToken)
    {
        List<OutboundEmailWork> batch = _buffer.Drain(MaxBatchSize);
        if (batch.Count == 0) return new BatchResult(0, Settled: false);

        List<OutboundEmailWork> routable = DropUnroutable(batch);
        if (routable.Count == 0) return new BatchResult(0, Settled: true);

        List<ComposedGroup> groups = [];
        List<Outgoing> outgoing = [];
        try
        {
            foreach (IGrouping<OutboundEmailKind, OutboundEmailWork> kind in routable.GroupBy(w => w.Kind))
            {
                ComposedEmails composed = await _composers[kind.Key].ComposeAsync(
                    [.. kind.Select(w => w.Id).Distinct()], cancellationToken);

                int group = groups.Count;
                groups.Add(new ComposedGroup(kind.Key, composed));
                for (int i = 0; i < composed.Mails.Count; i++)
                {
                    outgoing.Add(new Outgoing(
                        new OutboundEmailWork(kind.Key, composed.Mails[i].RestoreId), composed.Mails[i].Mail, group, i));
                }
            }
        }
        catch (Exception ex)
        {
            // Nothing was sent: the whole drained batch goes back.
            _buffer.Restore(routable);
            if (ex is not OperationCanceledException)
            {
                _logger.LogError(ex,
                    "Outbound email compose failed for {BatchSize} item(s); all {RestoredCount} restored for retry.",
                    routable.Count, routable.Count);
            }
            throw;
        }

        if (outgoing.Count == 0)
        {
            // Every drained item was ineligible: all of them are settled.
            _buffer.ForgetDeferrals(routable);
            return new BatchResult(0, Settled: true);
        }

        IReadOnlyList<int> deferredIndices;
        try
        {
            deferredIndices = await _transport.SendBatchAsync([.. outgoing.Select(o => o.Mail)], cancellationToken);
        }
        catch (Exception ex)
        {
            // The transport reports how many leading mails it finished with, and which of those the
            // server deferred; a transport that throws anything else is assumed to have processed
            // none (IMailTransport contract).
            var interrupted = ex as MailBatchInterruptedException;
            int processed = interrupted is null ? 0 : Math.Clamp(interrupted.ProcessedCount, 0, outgoing.Count);
            HashSet<int> deferredInHead = ValidIndices(interrupted?.DeferredIndices ?? [], processed);

            List<OutboundEmailWork> unsent = [.. outgoing.Skip(processed).Select(o => o.Restore).Distinct()];
            _buffer.Restore(unsent);
            HashSet<OutboundEmailWork> stillQueued = RequeueDeferred(outgoing, deferredInHead, alreadyRestored: unsent);
            stillQueued.UnionWith(unsent);
            _buffer.ForgetDeferrals(routable.Where(w => !stillQueued.Contains(w)));

            // The head went out: record it before reporting the failure, or a restored item of the same
            // conversation would mail its recipient a second time.
            await RunAfterSendAsync(groups, outgoing,
                Enumerable.Range(0, processed).Where(i => !deferredInHead.Contains(i)));

            Exception cause = interrupted is { InnerException: { } inner } ? inner : ex;
            if (cause is OperationCanceledException)
            {
                // A shutdown cancelled the send mid-batch. The unsent tail is back in the buffer;
                // rethrow the OCE itself so the caller's cancellation path (DrainAllAsync, the
                // worker) recognises it.
                _logger.LogInformation(
                    "Outbound email send cancelled after {ProcessedCount} of {BatchSize} mail(s); {RestoredCount} restored.",
                    processed, outgoing.Count, unsent.Count);
                ExceptionDispatchInfo.Capture(cause).Throw();
            }

            _logger.LogError(ex,
                "Outbound email send failed after {ProcessedCount} of {BatchSize} mail(s); {RestoredCount} restored for retry.",
                processed, outgoing.Count, unsent.Count);
            throw;
        }

        HashSet<int> deferred = ValidIndices(deferredIndices, outgoing.Count);
        HashSet<OutboundEmailWork> requeued = RequeueDeferred(outgoing, deferred, alreadyRestored: []);
        _buffer.ForgetDeferrals(routable.Where(w => !requeued.Contains(w)));
        await RunAfterSendAsync(groups, outgoing,
            Enumerable.Range(0, outgoing.Count).Where(i => !deferred.Contains(i)));

        return new BatchResult(outgoing.Count - deferred.Count, Settled: requeued.Count < batch.Count);
    }

    /// <summary>
    /// Splits off items whose kind has no registered composer. They are dropped — counted and logged
    /// at Error, never silently — because no flush could ever send them: it is a registration bug.
    /// </summary>
    private List<OutboundEmailWork> DropUnroutable(List<OutboundEmailWork> batch)
    {
        List<OutboundEmailWork> unroutable = [.. batch.Where(w => !_composers.ContainsKey(w.Kind))];
        if (unroutable.Count == 0) return batch;

        _buffer.ForgetDeferrals(unroutable);
        _logger.LogError(
            "Dropped {DroppedCount} outbound email item(s) of kind(s) {EmailKinds}: no composer is registered for them.",
            unroutable.Count, string.Join(", ", unroutable.Select(w => w.Kind).Distinct()));
        return [.. batch.Where(w => _composers.ContainsKey(w.Kind))];
    }

    private static HashSet<int> ValidIndices(IEnumerable<int> indices, int limit) =>
        [.. indices.Where(i => i >= 0 && i < limit)];

    /// <summary>
    /// Hands the mails the server deferred back to the buffer, which counts each restore key's deferrals
    /// and gives up on it at <see cref="OutboundEmailBuffer.MaxDeferrals"/>. A mail given up on is
    /// counted on <c>canalave.email.failed</c> and logged here (the transport left the deferral to its
    /// caller), so no mail ever disappears silently. A key already restored as part of the unsent tail
    /// is not deferred again.
    /// </summary>
    /// <returns>The items actually requeued (deferred and still under the cap).</returns>
    private HashSet<OutboundEmailWork> RequeueDeferred(
        List<Outgoing> outgoing, HashSet<int> deferredIndices, IReadOnlyCollection<OutboundEmailWork> alreadyRestored)
    {
        if (deferredIndices.Count == 0) return [];

        List<Outgoing> deferred = [.. deferredIndices.Order().Select(i => outgoing[i])];
        List<OutboundEmailWork> keys = [.. deferred.Select(d => d.Restore).Distinct().Where(k => !alreadyRestored.Contains(k))];
        HashSet<OutboundEmailWork> exhausted = [.. _buffer.Defer(keys)];

        foreach (Outgoing given in deferred.Where(d => exhausted.Contains(d.Restore)))
        {
            CanalaveTelemetry.Email.Failed.Add(1, new KeyValuePair<string, object?>("canalave.email.kind", given.Mail.Kind));
            _logger.LogError(
                "Email {EmailKind} to user {UserId} was deferred by the SMTP server {MaxDeferrals} times; dropped (mail is a side channel: the row it announced stands).",
                given.Mail.Kind, given.Mail.UserId, OutboundEmailBuffer.MaxDeferrals);
        }

        HashSet<OutboundEmailWork> requeued = [.. keys.Where(k => !exhausted.Contains(k))];
        if (requeued.Count > 0)
        {
            _logger.LogWarning(
                "{DeferredCount} outbound email(s) deferred by the SMTP server (a 4xx to the recipient); requeued for retry.",
                requeued.Count);
        }
        return requeued;
    }

    /// <summary>
    /// Calls each composer's after-send hook with the indices of its mails the transport took. Runs
    /// with <see cref="CancellationToken.None"/> (the mail has already left), and a failing hook is
    /// logged at Error and never restores anything — a restore would send the mail a second time.
    /// </summary>
    private async Task RunAfterSendAsync(List<ComposedGroup> groups, List<Outgoing> outgoing, IEnumerable<int> takenIndices)
    {
        foreach (IGrouping<int, int> taken in takenIndices.GroupBy(i => outgoing[i].Group))
        {
            ComposedGroup group = groups[taken.Key];
            if (group.Emails.AfterSendAsync is not { } afterSend) continue;

            List<int> indices = [.. taken.Select(i => outgoing[i].IndexInGroup)];
            try
            {
                await afterSend(indices, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "After-send step for {MailCount} {EmailKind} mail(s) failed; the mail was sent and is not restored.",
                    indices.Count, group.Kind);
            }
        }
    }

    /// <summary>
    /// Drains the buffer to empty, one bounded batch at a time — the graceful-shutdown path
    /// (<see cref="OutboundEmailWorker"/>). A single <see cref="FlushAsync"/> moves at most
    /// <see cref="MaxBatchSize"/> items, so a shutdown that flushed once would strand the rest of a
    /// deep queue. Lives here, not on the worker, so Integration tests drive it deterministically.
    ///
    /// <para>Stops at the first failed batch (that batch is already restored — retrying a dead SMTP
    /// host in a loop would only spin until the budget ran out), at a batch that settled nothing
    /// (everything it drained was deferred again, so what is left is mail the server is deferring, and
    /// retrying it at once would only repeat the deferral), or when <paramref name="budget"/> is
    /// cancelled. Never throws.</para>
    /// </summary>
    /// <returns>The number of items still queued when it stopped (0 = fully drained).</returns>
    public async Task<int> DrainAllAsync(CancellationToken budget)
    {
        try
        {
            while (_buffer.Count > 0)
            {
                budget.ThrowIfCancellationRequested();
                if (!(await FlushBatchAsync(budget)).Settled)
                    break;
            }
        }
        catch (OperationCanceledException) when (budget.IsCancellationRequested)
        {
            // The budget ran out. FlushAsync restored any batch it was holding; the caller logs the
            // stranded count this returns.
        }
        catch (Exception ex)
        {
            // FlushAsync has already logged this failure (Error, with the batch) and restored the batch.
            _logger.LogWarning(
                "Outbound email drain stopped after a failed batch ({ExceptionType}); {QueuedCount} still queued.",
                ex.GetType().Name, _buffer.Count);
        }

        return _buffer.Count;
    }
}
