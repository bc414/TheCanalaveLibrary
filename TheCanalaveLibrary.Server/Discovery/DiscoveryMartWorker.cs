using Microsoft.EntityFrameworkCore;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Hosted driver for the daily discovery-mart rebuild (layer8-data-marts.md): ensures the live
/// tables exist at startup, rebuilds immediately when the tree mart is empty (dev/first-deploy
/// friendliness), then rebuilds all three marts sequentially once per day at the configured
/// off-hours hour (<c>Marts:RebuildHourUtc</c>, default 03:00 UTC).
///
/// A failed rebuild is logged as Error and the loop continues — the previous live table keeps
/// serving (the whole point of the staging swap); the next scheduled pass retries.
/// TestAppFactory removes this worker so integration tests rebuild deterministically via
/// <see cref="DiscoveryMartRebuilder"/> directly (same treatment as the signal-buffer flush workers).
/// </summary>
public sealed class DiscoveryMartWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<DiscoveryMartWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let startup (migrations, seeding) settle before touching the database.
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            DiscoveryMartRebuilder rebuilder = scope.ServiceProvider.GetRequiredService<DiscoveryMartRebuilder>();
            await rebuilder.EnsureLiveTablesAsync(stoppingToken);

            if (await IsTreeMartEmptyAsync(scope, stoppingToken))
            {
                logger.LogInformation("Discovery marts empty at startup — running initial rebuild");
                await RebuildAllAsync(rebuilder, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Initial discovery-mart bootstrap failed; daily schedule continues");
        }

        int rebuildHourUtc = configuration.GetValue("Marts:RebuildHourUtc", 3);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(DelayUntilNext(rebuildHourUtc, DateTime.UtcNow), stoppingToken);

                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                DiscoveryMartRebuilder rebuilder = scope.ServiceProvider.GetRequiredService<DiscoveryMartRebuilder>();
                await RebuildAllAsync(rebuilder, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Previous live tables keep serving; retry at the next scheduled hour.
                logger.LogError(ex, "Daily discovery-mart rebuild failed; previous mart data remains live");
            }
        }
    }

    private async Task RebuildAllAsync(DiscoveryMartRebuilder rebuilder, CancellationToken ct)
    {
        (long treeEdges, long alsoFavorited, long alsoRecommended) = await rebuilder.RebuildAllAsync(ct);
        logger.LogInformation(
            "Discovery marts rebuilt: {TreeEdgeCount} tree edges, {AlsoFavoritedCount} also-favorited pairs, {AlsoRecommendedCount} also-recommended pairs",
            treeEdges, alsoFavorited, alsoRecommended);
    }

    private static async Task<bool> IsTreeMartEmptyAsync(AsyncServiceScope scope, CancellationToken ct)
    {
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        List<long> counts = await context.Database
            .SqlQueryRaw<long>($"SELECT COUNT(*) AS \"Value\" FROM {DiscoveryMartSchema.TreeSearchTable}")
            .ToListAsync(ct);
        return counts.Single() == 0;
    }

    /// <summary>Time until the next occurrence of <paramref name="hourUtc"/>:00 UTC (tomorrow if
    /// already past today). Internal for unit testing.</summary>
    internal static TimeSpan DelayUntilNext(int hourUtc, DateTime nowUtc)
    {
        DateTime next = nowUtc.Date.AddHours(hourUtc);
        if (next <= nowUtc) next = next.AddDays(1);
        return next - nowUtc;
    }
}
