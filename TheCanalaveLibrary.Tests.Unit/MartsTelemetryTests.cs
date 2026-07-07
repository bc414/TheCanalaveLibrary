using FluentAssertions;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Metric-shape tests for the mart-rebuild instrumentation (AD8, logging.md §"Custom
/// instrumentation"). <c>RecordRebuild</c> is the single helper every rebuilder calls so the
/// tag shape can't drift between marts — assert it here once (pattern:
/// <see cref="ImageStorageTelemetryTests"/>). The rebuild spans + real SQL are covered in
/// Integration (DiscoveryMartTests).
/// </summary>
public sealed class MartsTelemetryTests
{
    [Fact]
    public void RecordRebuild_OnSuccess_RecordsDurationRowsAndSuccessfulSwap()
    {
        using MetricCollector<double> duration = new(CanalaveTelemetry.Marts.RebuildDuration);
        using MetricCollector<long> rows = new(CanalaveTelemetry.Marts.RebuildRows);
        using MetricCollector<long> swaps = new(CanalaveTelemetry.Marts.SwapOutcomes);

        CanalaveTelemetry.Marts.RecordRebuild("also_favorited_scores", durationMs: 123.4, rows: 42, success: true);

        CollectedMeasurement<double> durationPoint = duration.GetMeasurementSnapshot().Should().ContainSingle().Which;
        durationPoint.Value.Should().Be(123.4);
        durationPoint.Tags["canalave.mart.name"].Should().Be("also_favorited_scores");

        CollectedMeasurement<long> rowsPoint = rows.GetMeasurementSnapshot().Should().ContainSingle().Which;
        rowsPoint.Value.Should().Be(42);
        rowsPoint.Tags["canalave.mart.name"].Should().Be("also_favorited_scores");

        CollectedMeasurement<long> swapPoint = swaps.GetMeasurementSnapshot().Should().ContainSingle().Which;
        swapPoint.Value.Should().Be(1);
        swapPoint.Tags["canalave.mart.name"].Should().Be("also_favorited_scores");
        swapPoint.Tags["canalave.mart.success"].Should().Be(true);
    }

    [Fact]
    public void RecordRebuild_OnFailure_TagsTheSwapUnsuccessful()
    {
        using MetricCollector<long> swaps = new(CanalaveTelemetry.Marts.SwapOutcomes);

        CanalaveTelemetry.Marts.RecordRebuild("user_story_tree_search_entries", durationMs: 5, rows: 0, success: false);

        CollectedMeasurement<long> swapPoint = swaps.GetMeasurementSnapshot().Should().ContainSingle().Which;
        swapPoint.Tags["canalave.mart.success"].Should().Be(false);
    }
}
