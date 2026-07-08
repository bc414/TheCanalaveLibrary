using System.Diagnostics;
using System.Text.Json;
using Npgsql;
using TheCanalaveLibrary.PerfBaseline;

// ── WU-L6 perf smoke baseline ──────────────────────────────────────────────────────────────────
//   dotnet run --project TheCanalaveLibrary.PerfBaseline -- --label before   # run + save
//   dotnet run --project TheCanalaveLibrary.PerfBaseline -- --label after
//   dotnet run --project TheCanalaveLibrary.PerfBaseline -- --compare before after
// Results + EXPLAIN plans land in TheCanalaveLibrary.PerfBaseline/results/.
// Run against the SeedTool extended dataset (toy volume can't exercise the planner).

const string DefaultConnection =
    "Server=localhost;Port=5432;Database=TheCanalaveLibraryDB;User Id=postgres;Password=butterfree;";
const int WarmupIterations = 3;
const int MeasuredIterations = 40;

string? label = null, compareA = null, compareB = null;
string connectionString = DefaultConnection;
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--label": label = args[++i]; break;
        case "--compare": compareA = args[++i]; compareB = args[++i]; break;
        case "--connection": connectionString = args[++i]; break;
        default: Console.Error.WriteLine($"Unknown argument {args[i]}"); return 2;
    }
}

string resultsDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "results");
resultsDir = Path.GetFullPath(resultsDir);
Directory.CreateDirectory(resultsDir);

if (compareA is not null && compareB is not null) return Compare(resultsDir, compareA, compareB);
if (label is null) { Console.Error.WriteLine("Pass --label <name> or --compare <a> <b>."); return 2; }

await using NpgsqlConnection connection = new(connectionString);
await connection.OpenAsync();

Dictionary<string, ScenarioResult> results = [];
string explainDir = Path.Combine(resultsDir, $"explain-{label}");
Directory.CreateDirectory(explainDir);

foreach (SqlScenario scenario in Scenarios.All)
{
    // Deterministic hot-id pool.
    List<object> pool = [];
    await using (NpgsqlCommand poolCommand = new(scenario.ParameterPoolSql, connection) { CommandTimeout = 300 })
    await using (NpgsqlDataReader reader = await poolCommand.ExecuteReaderAsync())
    {
        while (await reader.ReadAsync()) pool.Add(reader.GetValue(0));
    }
    if (pool.Count == 0)
    {
        Console.WriteLine($"{scenario.Name,-38} SKIPPED (empty parameter pool — seed the data first)");
        continue;
    }

    bool parameterless = !scenario.Sql.Contains("@id");

    async Task<double> RunOnceAsync(int iteration)
    {
        await using NpgsqlCommand command = new(scenario.Sql, connection) { CommandTimeout = 300 };
        if (!parameterless)
            command.Parameters.Add(new NpgsqlParameter(scenario.ParameterName, pool[iteration % pool.Count]));
        long start = Stopwatch.GetTimestamp();
        await using NpgsqlDataReader r = await command.ExecuteReaderAsync();
        while (await r.ReadAsync()) { } // drain — timing covers full result consumption
        return Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }

    for (int i = 0; i < WarmupIterations; i++) await RunOnceAsync(i);
    double[] samples = new double[MeasuredIterations];
    for (int i = 0; i < MeasuredIterations; i++) samples[i] = await RunOnceAsync(i);
    Array.Sort(samples);

    ScenarioResult result = new(
        Median: Percentile(samples, 50), P95: Percentile(samples, 95),
        Min: samples[0], Max: samples[^1], Iterations: MeasuredIterations);
    results[scenario.Name] = result;
    Console.WriteLine($"{scenario.Name,-38} p50 {result.Median,8:F2} ms   p95 {result.P95,8:F2} ms   " +
                      $"min {result.Min,7:F2}   max {result.Max,8:F2}");

    // EXPLAIN (ANALYZE, BUFFERS) once, on the hottest parameter — the attribution artifact.
    await using NpgsqlCommand explainCommand = new($"EXPLAIN (ANALYZE, BUFFERS) {scenario.Sql}", connection)
    {
        CommandTimeout = 300,
    };
    if (!parameterless)
        explainCommand.Parameters.Add(new NpgsqlParameter(scenario.ParameterName, pool[0]));
    List<string> plan = [$"-- {scenario.Name}", $"-- provenance: {scenario.Provenance}", ""];
    await using (NpgsqlDataReader planReader = await explainCommand.ExecuteReaderAsync())
    {
        while (await planReader.ReadAsync()) plan.Add(planReader.GetString(0));
    }
    await File.WriteAllLinesAsync(Path.Combine(explainDir, $"{scenario.Name}.txt"), plan);
}

string resultPath = Path.Combine(resultsDir, $"{label}.json");
await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(
    results, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"\nSaved {results.Count} scenarios → {resultPath}\nEXPLAIN plans → {explainDir}");
return 0;

static double Percentile(double[] sorted, int percentile)
{
    double rank = (percentile / 100.0) * (sorted.Length - 1);
    int low = (int)Math.Floor(rank);
    int high = (int)Math.Ceiling(rank);
    return low == high ? sorted[low] : sorted[low] + (rank - low) * (sorted[high] - sorted[low]);
}

static int Compare(string resultsDir, string labelA, string labelB)
{
    Dictionary<string, ScenarioResult> a = Load(resultsDir, labelA), b = Load(resultsDir, labelB);
    Console.WriteLine($"{"scenario",-38} {labelA + " p50",12} {labelB + " p50",12} {"Δ",10}   {labelA + " p95",12} {labelB + " p95",12}");
    foreach ((string name, ScenarioResult before) in a.OrderBy(kv => kv.Key))
    {
        if (!b.TryGetValue(name, out ScenarioResult? after)) continue;
        double delta = before.Median <= 0 ? 0 : (after.Median - before.Median) / before.Median * 100;
        Console.WriteLine($"{name,-38} {before.Median,10:F2}ms {after.Median,10:F2}ms {delta,9:+0.0;-0.0}%   " +
                          $"{before.P95,10:F2}ms {after.P95,10:F2}ms");
    }
    return 0;

    static Dictionary<string, ScenarioResult> Load(string dir, string label) =>
        JsonSerializer.Deserialize<Dictionary<string, ScenarioResult>>(
            File.ReadAllText(Path.Combine(dir, $"{label}.json")))!;
}

public sealed record ScenarioResult(double Median, double P95, double Min, double Max, int Iterations);
