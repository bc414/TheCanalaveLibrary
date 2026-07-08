using Microsoft.AspNetCore.Identity;
using Npgsql;
using TheCanalaveLibrary.SeedTool;

// ── The Canalave Library extended-seed tool (WU-Marts) ─────────────────────────────────────────
// Generates synthetic-but-realistically-clustered discovery data and bulk-loads it via binary
// COPY. Run against the PERSISTENT dev database only (local :5432 default, Aspire :5433 via
// --connection). Never part of app startup or the test suite.
//
//   dotnet run --project TheCanalaveLibrary.SeedTool -- [--seed 1337] [--users 2000]
//       [--stories 3000] [--communities 8] [--gem-chains 12] [--connection "<npgsql conn>"]
//
// The tool refuses to run twice (marker: any 'seed-user-%' user). To regenerate, wipe first:
//   scripts/reset-dev-db.ps1 -Restart   (recreates schema + dev seed), then re-run this tool.
// All seed users share the password "Password123!".

const string DefaultConnection =
    "Server=localhost;Port=5432;Database=TheCanalaveLibraryDB;User Id=postgres;Password=butterfree;";

SeedToolOptions options;
try
{
    options = ParseArgs(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Argument error: {ex.Message}");
    return 2;
}

Console.WriteLine($"Extended seed: seed={options.Seed} users={options.Users} stories={options.Stories} " +
                  $"communities={options.Communities} gem-chains={options.HiddenGemChains}");

await using NpgsqlConnection connection = new(options.ConnectionString);
try
{
    await connection.OpenAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Cannot connect: {ex.Message}");
    Console.Error.WriteLine("Is the dev Postgres running? (local :5432, or pass --connection for Aspire :5433)");
    return 1;
}

// ── Preflight ──────────────────────────────────────────────────────────────────────────────────
try
{
    long seedMarkerCount = (long)(await Scalar("SELECT COUNT(*) FROM \"AspNetUsers\" WHERE user_name LIKE 'seed-user-%'"))!;
    if (seedMarkerCount > 0)
    {
        Console.Error.WriteLine(
            $"Database already contains {seedMarkerCount} extended-seed users. This tool never tops up " +
            "(deterministic single-shot, mirroring DataSeeder's rule) — wipe first: scripts/reset-dev-db.ps1 -Restart");
        return 1;
    }
    long themeCount = (long)(await Scalar("SELECT COUNT(*) FROM themes WHERE theme_id = 1"))!;
    if (themeCount == 0)
    {
        Console.Error.WriteLine("Lookup seed data missing (theme 1). Start the app once so migrations + HasData run.");
        return 1;
    }
}
catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
{
    Console.Error.WriteLine("Schema missing. Run migrations first (start the app once, or scripts/reset-dev-db.ps1 -Restart).");
    return 1;
}

SeedIdBases bases = new(
    UserId: 1 + Convert.ToInt32(await Scalar("SELECT COALESCE(MAX(id), 0) FROM \"AspNetUsers\"")),
    StoryId: 1 + Convert.ToInt32(await Scalar("SELECT COALESCE(MAX(story_id), 0) FROM stories")),
    ChapterId: 1 + Convert.ToInt32(await Scalar("SELECT COALESCE(MAX(chapter_id), 0) FROM chapters")),
    ChapterContentId: 1 + Convert.ToInt64(await Scalar("SELECT COALESCE(MAX(chapter_content_id), 0) FROM chapter_contents")),
    RecommendationId: 1 + Convert.ToInt32(await Scalar("SELECT COALESCE(MAX(recommendation_id), 0) FROM recommendations")),
    CommentId: 1 + Convert.ToInt64(await Scalar("SELECT COALESCE(MAX(comment_id), 0) FROM base_comments")),
    NotificationId: 1 + Convert.ToInt64(await Scalar("SELECT COALESCE(MAX(notification_id), 0) FROM notifications")));

// ── Generate + load ────────────────────────────────────────────────────────────────────────────
var stopwatch = System.Diagnostics.Stopwatch.StartNew();
SeedGraph graph = new SeedGraphGenerator(options, bases).Generate();
Console.WriteLine($"Generated in {stopwatch.Elapsed.TotalSeconds:F1}s: {graph.Users.Count} users, " +
                  $"{graph.Stories.Count} stories, {graph.Chapters.Count} chapters, " +
                  $"{graph.Interactions.Count} interactions, {graph.Recommendations.Count} recommendations " +
                  $"({graph.Recommendations.Count(r => r.IsHiddenGem)} gems / " +
                  $"{graph.Recommendations.Count(r => r.IsHighlightedByAuthor)} spotlights / " +
                  $"{graph.Recommendations.Count(r => r.RecommenderId is null)} anonymized), " +
                  $"{graph.Vouches.Count} vouches, {graph.HiddenGemChainCount} gem chains, " +
                  $"{graph.ChapterComments.Count} chapter comments ({graph.ChapterComments.Count(c => c.ParentCommentId is not null)} replies), " +
                  $"{graph.Notifications.Count} notifications");

// One PBKDF2 hash shared by every seed user — hashing per-user is the known cost DataSeeder's
// Minimal mode exists to avoid; all seed users share "Password123!" anyway.
string passwordHash = new PasswordHasher<object>().HashPassword(null!, "Password123!");

stopwatch.Restart();
await new SeedBulkWriter(connection).WriteAsync(graph, passwordHash);
Console.WriteLine($"Loaded via COPY in {stopwatch.Elapsed.TotalSeconds:F1}s.");

// ── Post-load spot checks (the D3 bar: rankable, not just non-empty) ──────────────────────────
Console.WriteLine("\nCo-occurrence spot check (top shared-favorite pairs — non-uniform scores expected):");
await using (NpgsqlCommand command = new("""
    WITH fav AS (SELECT user_id, story_id FROM user_story_interactions WHERE is_favorite = true)
    SELECT a.story_id, b.story_id, COUNT(*) AS score
    FROM fav a JOIN fav b ON b.user_id = a.user_id AND b.story_id <> a.story_id
    GROUP BY a.story_id, b.story_id ORDER BY score DESC LIMIT 5
    """, connection) { CommandTimeout = 300 })
await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync())
{
    while (await reader.ReadAsync())
        Console.WriteLine($"  stories {reader.GetInt32(0)} ↔ {reader.GetInt32(1)}: {reader.GetInt64(2)} shared users");
}

Console.WriteLine("\nDone. Next: start the app and POST /dev/marts/rebuild (or wait for the daily worker), " +
                  "then GET /dev/discovery/tree-search and /dev/discovery/also-favorited/{storyId}.");
return 0;

async Task<object?> Scalar(string sql)
{
    await using NpgsqlCommand command = new(sql, connection) { CommandTimeout = 300 };
    return await command.ExecuteScalarAsync();
}

static SeedToolOptions ParseArgs(string[] args)
{
    string connection = DefaultConnection;
    int seed = 1337, users = 2000, stories = 3000, communities = 8, gemChains = 12;
    for (int i = 0; i < args.Length; i++)
    {
        string Next() => i + 1 < args.Length
            ? args[++i]
            : throw new ArgumentException($"{args[i]} requires a value");
        switch (args[i])
        {
            case "--connection": connection = Next(); break;
            case "--seed": seed = int.Parse(Next()); break;
            case "--users": users = int.Parse(Next()); break;
            case "--stories": stories = int.Parse(Next()); break;
            case "--communities": communities = int.Parse(Next()); break;
            case "--gem-chains": gemChains = int.Parse(Next()); break;
            default: throw new ArgumentException($"Unknown argument: {args[i]}");
        }
    }
    if (users < 10 || stories < 10 || communities < 1)
        throw new ArgumentException("Need at least 10 users, 10 stories, 1 community.");
    return new SeedToolOptions
    {
        ConnectionString = connection,
        Seed = seed,
        Users = users,
        Stories = stories,
        Communities = communities,
        HiddenGemChains = gemChains,
    };
}
