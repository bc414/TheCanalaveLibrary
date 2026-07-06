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

    // Later work-units add their component here (never a new top-level source elsewhere):
    //   WU-Redis  → ViewCount ("TheCanalaveLibrary.ViewCount"): queue-depth gauge,
    //               drain batch-size + duration histograms, drain-cycle spans.
    //   WU-Email  → Email ("TheCanalaveLibrary.Email"): Email.Send spans, sent/failed counters.
    //   WU-Marts  → Marts ("TheCanalaveLibrary.Marts"): Mart.Rebuild spans + duration histogram.
}
