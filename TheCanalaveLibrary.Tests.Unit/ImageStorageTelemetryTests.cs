using System.Diagnostics;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Telemetry-emission tests for the image-storage custom instrumentation (WU-Observability's
/// pilot — the worked example logging.md §"Custom Instrumentation" points at). Uses the Local
/// impl because it is directly constructible (fake <see cref="IWebHostEnvironment"/> over a temp
/// folder — no host, no DB, per testing.md's placement rule); the S3 impl emits through the
/// identical span/metric/log shape (shared <c>CanalaveTelemetry.ImageStorage</c> instruments +
/// <c>RecordUpload</c>), and its storage round-trip is covered in Integration against Garage.
///
/// Pattern reference for later components (ViewCount, Email, Marts):
/// <see cref="ActivityListener"/> filtered to the component's source name asserts spans;
/// <see cref="MetricCollector{T}"/> over the static instruments asserts measurements;
/// <see cref="FakeLogger{T}"/> asserts level + structured properties.
/// </summary>
public sealed class ImageStorageTelemetryTests : IDisposable
{
    // Minimal valid 1x1 PNG — same fixture bytes as the Integration image suites.
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    private readonly string _webRoot =
        Path.Combine(Path.GetTempPath(), "canalave-telemetry-tests", Guid.NewGuid().ToString("N"));

    private readonly FakeLogger<LocalImageStorageService> _logger = new();

    private LocalImageStorageService BuildSut() => new(new FakeWebHostEnvironment(_webRoot), _logger);

    public void Dispose()
    {
        if (Directory.Exists(_webRoot))
        {
            Directory.Delete(_webRoot, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_EmitsSaveActivity_TaggedWithProviderKindAndSize()
    {
        List<Activity> captured = [];
        using ActivityListener listener = ListenTo(CanalaveTelemetry.ImageStorage.Name, captured);
        LocalImageStorageService sut = BuildSut();
        using MemoryStream content = new(OnePixelPng);

        await sut.SaveAsync(content, "image/png", ImageKind.Cover, ownerId: 1);

        Activity activity = captured.Should().ContainSingle().Which;
        activity.OperationName.Should().Be("ImageStorage.Save");
        activity.GetTagItem("canalave.image.provider").Should().Be("local");
        activity.GetTagItem("canalave.image.kind").Should().Be("Cover");
        activity.GetTagItem("canalave.image.size_bytes").Should().Be((long)OnePixelPng.Length);
        activity.Status.Should().Be(ActivityStatusCode.Unset, "a successful save is not an error span");

        FakeLogRecord log = _logger.Collector.GetSnapshot().Should().ContainSingle().Which;
        log.Level.Should().Be(LogLevel.Information);
        log.StructuredState.Should().Contain(kv => kv.Key == "ImageKind")
            .And.Contain(kv => kv.Key == "SizeBytes");
    }

    [Fact]
    public async Task SaveAsync_RecordsUploadCounterAndSizeHistogram_WithKindAndProviderTags()
    {
        using MetricCollector<long> uploads = new(CanalaveTelemetry.ImageStorage.Uploads);
        using MetricCollector<long> sizes = new(CanalaveTelemetry.ImageStorage.UploadSize);
        LocalImageStorageService sut = BuildSut();
        using MemoryStream content = new(OnePixelPng);

        await sut.SaveAsync(content, "image/png", ImageKind.ProfilePicture, ownerId: 7);

        CollectedMeasurement<long> count = uploads.GetMeasurementSnapshot().Should().ContainSingle().Which;
        count.Value.Should().Be(1);
        count.Tags["canalave.image.kind"].Should().Be("ProfilePicture");
        count.Tags["canalave.image.provider"].Should().Be("local");

        CollectedMeasurement<long> size = sizes.GetMeasurementSnapshot().Should().ContainSingle().Which;
        size.Value.Should().Be(OnePixelPng.Length);
        size.Tags.Should().ContainKey("canalave.image.kind");
    }

    [Fact]
    public async Task SaveAsync_OnOversizedContent_MarksTheActivityAsError_AndRecordsNoUpload()
    {
        List<Activity> captured = [];
        using ActivityListener listener = ListenTo(CanalaveTelemetry.ImageStorage.Name, captured);
        using MetricCollector<long> uploads = new(CanalaveTelemetry.ImageStorage.Uploads);
        LocalImageStorageService sut = BuildSut();
        using MemoryStream content = new(new byte[ImageUploadRules.MaxBytes + 1]);

        Func<Task> act = () => sut.SaveAsync(content, "image/png", ImageKind.Cover, ownerId: 1);

        await act.Should().ThrowAsync<ArgumentException>();
        Activity activity = captured.Should().ContainSingle().Which;
        activity.Status.Should().Be(ActivityStatusCode.Error);
        activity.Events.Should().Contain(e => e.Name == "exception",
            "the failure is recorded on the span, not double-logged (logging.md)");
        uploads.GetMeasurementSnapshot().Should().BeEmpty("only completed uploads count");
    }

    [Fact]
    public async Task DeleteAsync_OnAPathThisServiceNeverProduced_LogsWarningAndNoOps()
    {
        List<Activity> captured = [];
        using ActivityListener listener = ListenTo(CanalaveTelemetry.ImageStorage.Name, captured);
        LocalImageStorageService sut = BuildSut();

        // A stored value the upload path never writes (e.g. a seeded /images/… URL) — the
        // interface contract says no-op, the telemetry contract says leave a trace of it.
        await sut.DeleteAsync("/images/seeded-avatar.png");

        FakeLogRecord log = _logger.Collector.GetSnapshot().Should().ContainSingle().Which;
        log.Level.Should().Be(LogLevel.Warning);
        log.Message.Should().Contain("/images/seeded-avatar.png");

        Activity activity = captured.Should().ContainSingle().Which;
        activity.OperationName.Should().Be("ImageStorage.Delete");
        activity.GetTagItem("canalave.image.delete_noop").Should().Be(true);
    }

    private static ActivityListener ListenTo(string sourceName, List<Activity> captured)
    {
        ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == sourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = captured.Add,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    /// <summary>Temp-folder WebRoot; nothing else on the interface is touched by the impl.</summary>
    private sealed class FakeWebHostEnvironment(string webRoot) : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = webRoot;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = webRoot;
        public string EnvironmentName { get; set; } = "Development";
    }
}
