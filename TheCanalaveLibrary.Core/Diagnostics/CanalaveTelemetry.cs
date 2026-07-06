using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Central registry of the app's custom telemetry producers — one nested class per instrumented
/// component, each owning an <see cref="ActivitySource"/> (traces) and a <see cref="Meter"/>
/// (metrics) named <c>TheCanalaveLibrary.{Component}</c>. ServiceDefaults subscribes to all of
/// them at once via the wildcard <c>"TheCanalaveLibrary.*"</c> (see ServiceDefaults/Extensions.cs)
/// — adding a new component here lights it up with no registration change.
///
/// Producers are process singletons on purpose: <see cref="ActivitySource"/> and
/// <see cref="Meter"/> are thread-safe, stateless funnels (the mirror image of DbContext, which is
/// scoped precisely because it is stateful and not thread-safe). Never inject or scope these.
///
/// Lives in Core because these are in-box BCL types (no OpenTelemetry package dependency,
/// WASM-safe) — workers and the future WASM client emit against the same sources. The OTel SDK,
/// exporters, and all subscription stay in ServiceDefaults; Core only emits.
///
/// Emission idiom (null-safe — StartActivity returns null when nothing listens, e.g. unit tests):
/// <code>
/// using Activity? activity = CanalaveTelemetry.ImageStorage.Source.StartActivity("ImageStorage.Save");
/// activity?.SetTag("canalave.image.kind", kind);
/// </code>
/// Spans auto-parent under Activity.Current (the ambient framework circuit/request span) —
/// cross-source nesting (Components → yours → Npgsql/Http) is the expected trace shape.
///
/// Naming rules (canalave-conventions/logging.md §"Custom Instrumentation"):
/// span names <c>{Component}.{Operation}</c> PascalCase, IDs in tags never in names; metric names
/// <c>canalave.*</c> lowercase-dotted with UCUM units; tag names <c>canalave.{noun}.{property}</c>
/// matching log property names so traces and logs correlate.
/// </summary>
public static class CanalaveTelemetry
{
    /// <summary>Common prefix for every per-component source/meter name.</summary>
    public const string Prefix = "TheCanalaveLibrary";

    /// <summary>
    /// Image blob storage (S3/Garage/R2 + local filesystem) — the write path behind
    /// IImageStorageService. First custom-instrumented component (WU-Observability pilot).
    /// </summary>
    public static class ImageStorage
    {
        public const string Name = Prefix + ".ImageStorage";

        public static readonly ActivitySource Source = new(Name, "1.0.0");
        public static readonly Meter Meter = new(Name, "1.0.0");

        public static readonly Counter<long> Uploads = Meter.CreateCounter<long>(
            "canalave.image.uploads", unit: "{upload}",
            description: "Completed image blob uploads.");

        public static readonly Histogram<long> UploadSize = Meter.CreateHistogram<long>(
            "canalave.image.upload.size", unit: "By",
            description: "Size in bytes of completed image blob uploads.");

        /// <summary>One completed upload — both instruments, consistently tagged. Both storage
        /// impls call this so the metric shape can't drift between providers.</summary>
        public static void RecordUpload(ImageKind kind, string provider, long sizeBytes)
        {
            KeyValuePair<string, object?>[] tags =
            [
                new("canalave.image.kind", kind.ToString()),
                new("canalave.image.provider", provider),
            ];
            Uploads.Add(1, tags);
            UploadSize.Record(sizeBytes, tags);
        }
    }

    /// <summary>
    /// Reading-progress in-process write buffer (Feature 44 L2 — the signal-buffering pattern,
    /// layer2-services.md). A write buffer is trustworthy only when measurable: depth says how much
    /// is at risk in the loss window, batch size says how well coalescing works, duration says
    /// whether the flush keeps up with its cadence.
    /// </summary>
    public static class ReadingProgress
    {
        public const string Name = Prefix + ".ReadingProgress";

        public static readonly ActivitySource Source = new(Name, "1.0.0");
        public static readonly Meter Meter = new(Name, "1.0.0");

        public static readonly Histogram<int> FlushBatchSize = Meter.CreateHistogram<int>(
            "canalave.readingprogress.flush.batch_size", unit: "{entry}",
            description: "Coalesced (user, chapter) entries written per flush cycle.");

        public static readonly Histogram<double> FlushDuration = Meter.CreateHistogram<double>(
            "canalave.readingprogress.flush.duration", unit: "ms",
            description: "Wall time of one flush cycle's batched upsert.");

        // The buffer-depth ObservableGauge ("canalave.readingprogress.buffer.depth") is created by
        // ReadingProgressBuffer's constructor — a gauge needs the live buffer instance to observe,
        // and that singleton lives in the Server project.
    }

    /// <summary>
    /// Story view-count in-process write buffer (Feature 45 L2 — the signal-buffering pattern,
    /// layer2-services.md). Same instrument shape as <see cref="ReadingProgress"/>: depth = at-risk
    /// entries in the loss window, batch size = coalescing effectiveness, duration = flush health.
    /// </summary>
    public static class ViewCount
    {
        public const string Name = Prefix + ".ViewCount";

        public static readonly ActivitySource Source = new(Name, "1.0.0");
        public static readonly Meter Meter = new(Name, "1.0.0");

        public static readonly Histogram<int> FlushBatchSize = Meter.CreateHistogram<int>(
            "canalave.viewcount.flush.batch_size", unit: "{story}",
            description: "Distinct stories whose coalesced views were written per flush cycle.");

        public static readonly Histogram<double> FlushDuration = Meter.CreateHistogram<double>(
            "canalave.viewcount.flush.duration", unit: "ms",
            description: "Wall time of one flush cycle's batched upsert.");

        // The buffer-depth ObservableGauge ("canalave.viewcount.buffer.depth") is created by
        // ViewCountBuffer's constructor — same rationale as ReadingProgress's gauge.
    }

    /// <summary>
    /// Transactional email (WU-Email) — the send path behind <c>IEmailSender&lt;User&gt;</c>.
    /// HttpClient/socket auto-instrumentation is blind to SMTP, so this names "one transactional
    /// email" as its own span; sent/failed counters split by kind (confirmation vs. reset) so a
    /// provider outage or misconfiguration shows up as a metric, not just an exception trace.
    /// </summary>
    public static class Email
    {
        public const string Name = Prefix + ".Email";

        public static readonly ActivitySource Source = new(Name, "1.0.0");
        public static readonly Meter Meter = new(Name, "1.0.0");

        public static readonly Counter<long> Sent = Meter.CreateCounter<long>(
            "canalave.email.sent", unit: "{email}",
            description: "Transactional emails successfully sent.");

        public static readonly Counter<long> Failed = Meter.CreateCounter<long>(
            "canalave.email.failed", unit: "{email}",
            description: "Transactional emails that failed to send.");
    }

    // Later work-units add their component here (never a new top-level source elsewhere):
    //   WU-Marts  → Marts ("TheCanalaveLibrary.Marts"): Mart.Rebuild spans + duration histogram.
}
