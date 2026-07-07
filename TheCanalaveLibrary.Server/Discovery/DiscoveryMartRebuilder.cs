using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Rebuilds the three discovery marts (layer8-data-marts.md) via raw SQL on the write context:
/// fresh staging table → bulk INSERT build → index → atomic swap (see
/// <see cref="DiscoveryMartSchema"/> for the SQL and the index-rename rationale).
///
/// Scoped service, deliberately separate from the hosted <see cref="DiscoveryMartWorker"/> so
/// integration tests and the dev diagnostics endpoint can trigger a rebuild directly without
/// hosting timing. Instrumented per AD8: one root-capable span per rebuild
/// (<c>Marts.{Mart}Rebuild</c>) + duration / row-count / swap-outcome metrics
/// (<see cref="CanalaveTelemetry.Marts"/>). Failures record on the span and metric, then
/// rethrow — the WORKER decides to log-and-continue (the previous live table keeps serving).
/// </summary>
public sealed class DiscoveryMartRebuilder(ApplicationDbContext context)
{
    /// <summary>Mart builds can scan every interaction row — give them a worker-scale timeout.</summary>
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(10);

    public Task<long> RebuildTreeSearchAsync(CancellationToken ct = default) => RebuildAsync(
        "TreeSearchRebuild", DiscoveryMartSchema.TreeSearchTable,
        DiscoveryMartSchema.TreeSearchEnsureLive, DiscoveryMartSchema.TreeSearchCreateStaging,
        DiscoveryMartSchema.TreeSearchBuild, DiscoveryMartSchema.TreeSearchIndexStaging,
        DiscoveryMartSchema.TreeSearchSwap, ct);

    public Task<long> RebuildAlsoFavoritedAsync(CancellationToken ct = default) => RebuildAsync(
        "AlsoFavoritedRebuild", DiscoveryMartSchema.AlsoFavoritedTable,
        DiscoveryMartSchema.AlsoFavoritedEnsureLive, DiscoveryMartSchema.AlsoFavoritedCreateStaging,
        DiscoveryMartSchema.AlsoFavoritedBuild, DiscoveryMartSchema.AlsoFavoritedIndexStaging,
        DiscoveryMartSchema.AlsoFavoritedSwap, ct);

    public Task<long> RebuildAlsoRecommendedAsync(CancellationToken ct = default) => RebuildAsync(
        "AlsoRecommendedRebuild", DiscoveryMartSchema.AlsoRecommendedTable,
        DiscoveryMartSchema.AlsoRecommendedEnsureLive, DiscoveryMartSchema.AlsoRecommendedCreateStaging,
        DiscoveryMartSchema.AlsoRecommendedBuild, DiscoveryMartSchema.AlsoRecommendedIndexStaging,
        DiscoveryMartSchema.AlsoRecommendedSwap, ct);

    /// <summary>Rebuilds all three marts sequentially (one daily pass; no concurrent DDL races).</summary>
    public async Task<(long TreeEdges, long AlsoFavorited, long AlsoRecommended)> RebuildAllAsync(
        CancellationToken ct = default)
    {
        long tree = await RebuildTreeSearchAsync(ct);
        long fav = await RebuildAlsoFavoritedAsync(ct);
        long rec = await RebuildAlsoRecommendedAsync(ct);
        return (tree, fav, rec);
    }

    /// <summary>Idempotent live-table bootstrap: consumers must never hit an undefined table —
    /// an EMPTY mart is a valid pre-first-rebuild state, a MISSING one is not.</summary>
    public async Task EnsureLiveTablesAsync(CancellationToken ct = default)
    {
        context.Database.SetCommandTimeout(CommandTimeout);
        await context.Database.ExecuteSqlRawAsync(DiscoveryMartSchema.TreeSearchEnsureLive, ct);
        await context.Database.ExecuteSqlRawAsync(DiscoveryMartSchema.AlsoFavoritedEnsureLive, ct);
        await context.Database.ExecuteSqlRawAsync(DiscoveryMartSchema.AlsoRecommendedEnsureLive, ct);
    }

    private async Task<long> RebuildAsync(
        string operation, string martName,
        string ensureLive, string createStaging, string build, string indexStaging, string swap,
        CancellationToken ct)
    {
        using Activity? activity = CanalaveTelemetry.Marts.Source.StartActivity($"Marts.{operation}");
        activity?.SetTag("canalave.mart.name", martName);

        long startTimestamp = Stopwatch.GetTimestamp();
        context.Database.SetCommandTimeout(CommandTimeout);
        try
        {
            await context.Database.ExecuteSqlRawAsync(ensureLive, ct);
            await context.Database.ExecuteSqlRawAsync(createStaging, ct);
            await context.Database.ExecuteSqlRawAsync(build, ct);

            // martName is one of the DiscoveryMartSchema compile-time table constants, never
            // user input — the interpolation cannot inject.
#pragma warning disable EF1002
            long rows = (await context.Database
                .SqlQueryRaw<long>($"SELECT COUNT(*) AS \"Value\" FROM {martName}_staging")
                .ToListAsync(ct)).Single();
#pragma warning restore EF1002

            await context.Database.ExecuteSqlRawAsync(indexStaging, ct);
            await context.Database.ExecuteSqlRawAsync(swap, ct);

            double durationMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            CanalaveTelemetry.Marts.RecordRebuild(martName, durationMs, rows, success: true);
            activity?.SetTag("canalave.mart.rows", rows);
            return rows;
        }
        catch (Exception ex)
        {
            double durationMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            CanalaveTelemetry.Marts.RecordRebuild(martName, durationMs, rows: 0, success: false);
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
