using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server implementation of <see cref="ICoOccurrenceReadService"/> (Feature 61). Reads the
/// raw-SQL co-occurrence marts directly, ranked by score — the mart IS the cache (L7 dissolved).
/// The viewer's filters apply at read time, at the presentation join (AD7):
/// content rating (E/T always; M only when <see cref="IActiveUserContext.ShowMatureContent"/>),
/// story visibility (approved, not taken down), and the viewer's effective §8.7 interaction
/// exclusions for the AlsoFavorited / AlsoRecommended search modes.
///
/// The mart tables have no EF model, so reads go through plain ADO commands (Npgsql tracing
/// still instruments them). A missing mart table (worker never ran — should not happen, the
/// worker bootstraps the schema at startup) degrades to an empty result with a Warning log,
/// never an exception on a story page.
/// </summary>
public class ServerCoOccurrenceReadService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser,
    IDiscoveryDefaultsReadService discoveryDefaults,
    ILogger<ServerCoOccurrenceReadService> logger) : ICoOccurrenceReadService
{
    public Task<IReadOnlyList<RelatedStoryScoreDto>> GetAlsoFavoritedAsync(
        int storyId, int take = 10,
        IReadOnlyList<UserStoryInteractionTypeEnum>? excludedInteractions = null,
        CancellationToken ct = default) =>
        QueryAsync(DiscoveryMartSchema.AlsoFavoritedTable, "also_favorited_story_id",
            SiteSearchModes.AlsoFavorited, storyId, take, excludedInteractions, ct);

    public Task<IReadOnlyList<RelatedStoryScoreDto>> GetAlsoRecommendedAsync(
        int storyId, int take = 10,
        IReadOnlyList<UserStoryInteractionTypeEnum>? excludedInteractions = null,
        CancellationToken ct = default) =>
        QueryAsync(DiscoveryMartSchema.AlsoRecommendedTable, "also_recommended_story_id",
            SiteSearchModes.AlsoRecommended, storyId, take, excludedInteractions, ct);

    private async Task<IReadOnlyList<RelatedStoryScoreDto>> QueryAsync(
        string martTable, string relatedColumn, string searchModeKey, int storyId, int take,
        IReadOnlyList<UserStoryInteractionTypeEnum>? excludedInteractions,
        CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

        using Activity? activity = CanalaveTelemetry.Discovery.Source.StartActivity("Discovery.CoOccurrenceRead");
        activity?.SetTag("canalave.mart.name", martTable);
        long startTimestamp = Stopwatch.GetTimestamp();

        // null = resolve the viewer's §8.7 defaults internally (unchanged prior behavior);
        // non-null = caller (e.g. a live UserStoryInteractionFilter) overrides the default outright.
        IReadOnlyList<UserStoryInteractionTypeEnum> exclusions = excludedInteractions
            ?? await discoveryDefaults.GetDefaultExcludedInteractionsAsync(searchModeKey);

        // martTable / relatedColumn come from compile-time constants above, never user input.
        string sql = $"""
            SELECT m.{relatedColumn}, m.score
            FROM {martTable} m
            JOIN stories s ON s.story_id = m.{relatedColumn}
            WHERE m.story_id = @storyId
              AND {DiscoveryMartSchema.VisibleStory}
              AND s.rating <= @maxRating
              AND NOT EXISTS (
                  SELECT 1 FROM user_story_interactions x
                  WHERE x.user_id = @viewerId AND x.story_id = m.{relatedColumn}
                    AND ((@exFavorite AND x.is_favorite)
                      OR (@exHiddenFavorite AND x.is_hidden_favorite)
                      OR (@exFollowed AND x.is_followed)
                      OR (@exCompleted AND x.is_completed)
                      OR (@exReadItLater AND x.is_read_it_later)
                      OR (@exIgnored AND x.is_ignored)))
            ORDER BY m.score DESC, m.{relatedColumn}
            LIMIT @take
            """;

        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync(ct);
        await readDb.Database.OpenConnectionAsync(ct);
        DbConnection connection = readDb.Database.GetDbConnection();

        await using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add(new NpgsqlParameter("storyId", storyId));
        command.Parameters.Add(new NpgsqlParameter("take", take));
        command.Parameters.Add(new NpgsqlParameter("maxRating", (short)activeUser.MaxRating));
        command.Parameters.Add(new NpgsqlParameter("viewerId", activeUser.UserId ?? -1));
        AddExclusionParameters(command, exclusions);

        try
        {
            List<RelatedStoryScoreDto> results = [];
            await using DbDataReader reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                results.Add(new RelatedStoryScoreDto
                {
                    RelatedStoryId = reader.GetInt32(0),
                    Score = reader.GetInt32(1),
                });

            activity?.SetTag("canalave.cooccurrence.result_count", results.Count);
            return results;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            // Degraded read: the mart hasn't been bootstrapped yet. The story page must not 500
            // over a missing recommendation strip — return empty and flag it loudly.
            logger.LogWarning(ex,
                "Co-occurrence mart {MartTable} does not exist yet — returning empty related list for story {StoryId}",
                martTable, storyId);
            return [];
        }
        finally
        {
            CanalaveTelemetry.Discovery.CoOccurrenceReadDuration.Record(
                Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds,
                new KeyValuePair<string, object?>("canalave.mart.name", martTable));
        }
    }

    /// <summary>Maps the viewer's effective §8.7 exclusions onto the six flag parameters shared
    /// by every discovery read (one boolean parameter per <c>user_story_interactions</c> flag).</summary>
    internal static void AddExclusionParameters(
        DbCommand command, IReadOnlyList<UserStoryInteractionTypeEnum> exclusions)
    {
        command.Parameters.Add(new NpgsqlParameter("exFavorite", exclusions.Contains(UserStoryInteractionTypeEnum.Favorite)));
        command.Parameters.Add(new NpgsqlParameter("exHiddenFavorite", exclusions.Contains(UserStoryInteractionTypeEnum.PrivateFavorite)));
        command.Parameters.Add(new NpgsqlParameter("exFollowed", exclusions.Contains(UserStoryInteractionTypeEnum.Follow)));
        command.Parameters.Add(new NpgsqlParameter("exCompleted", exclusions.Contains(UserStoryInteractionTypeEnum.Complete)));
        command.Parameters.Add(new NpgsqlParameter("exReadItLater", exclusions.Contains(UserStoryInteractionTypeEnum.ReadLater)));
        command.Parameters.Add(new NpgsqlParameter("exIgnored", exclusions.Contains(UserStoryInteractionTypeEnum.Ignore)));
    }
}
