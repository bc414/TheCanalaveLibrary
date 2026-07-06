using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Guards the per-query tracing seam WU-Observability added: ServiceDefaults subscribes to the
/// ActivitySource literally named <c>"Npgsql"</c> (via <c>Npgsql.OpenTelemetry</c>'s
/// <c>AddNpgsql()</c>), and the L6 index baseline (middle_plan_v2 Phase 1 item 3) leans on those
/// spans for before/after evidence. If an Npgsql upgrade ever renamed its source or stopped
/// emitting, the subscription would break <i>silently</i> — no compile error, just empty traces.
/// This smoke asserts a real query through the real provider still emits under that name.
/// </summary>
[Collection("Postgres")]
public class NpgsqlTracingSmokeTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task DatabaseQueries_EmitActivities_UnderTheNpgsqlSourceName()
    {
        List<Activity> captured = [];
        using ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == "Npgsql",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = captured.Add,
        };
        ActivitySource.AddActivityListener(listener);

        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryReadService stories = scope.ServiceProvider.GetRequiredService<IStoryReadService>();
        await stories.GetRecentListingsAsync(1, 10); // empty result is fine — the query still runs

        captured.Should().NotBeEmpty(
            "ServiceDefaults' AddNpgsql() subscription depends on Npgsql emitting under the source name \"Npgsql\"");
    }
}
