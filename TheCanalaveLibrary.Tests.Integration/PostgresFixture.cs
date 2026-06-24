using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Starts one real, ephemeral Postgres container for the whole "Postgres" test collection (not per
/// test) — see testing.md "Integration tests run against real Postgres — never InMemory/SQLite."
/// Applies the actual <c>InitialSchema</c> migration once the container is up, then builds a
/// <see cref="Respawner"/> that tests call via <see cref="ResetAsync"/> before each test to restore
/// the DB to the migrated+lookup baseline. See testing.md "Integration tests reset between every test."
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("canalavedb_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private Respawner _respawner = null!;

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        // Database.MigrateAsync(), not EnsureCreated() — EnsureCreated skips the actual migration
        // files entirely and would silently let a broken migration pass. The activeUser dependency is
        // unused for a schema-only migration run, so an anonymous fake is fine here.
        await using ApplicationDbContext migrationContext = new(options, FakeActiveUserContext.Anonymous());
        await migrationContext.Database.MigrateAsync();

        // Build the Respawner once, after the schema exists. TablesToIgnore protects the rows seeded
        // by EF's OnModelCreating HasData — lookup tables (statuses, types, roles, themes) that every
        // test relies on as FK targets without reseeding. Application rows (users, stories, etc.) are
        // wiped on every ResetAsync call so each test starts clean.
        await using NpgsqlConnection conn = new(ConnectionString);
        await conn.OpenAsync();
        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore =
            [
                "__EFMigrationsHistory",
                "asp_net_roles",
                "badges",
                "notification_categories",
                "notification_types",
                "recommendation_statuses",
                "search_modes",
                "user_story_interaction_filter_types",
                "default_user_story_interaction_filter_settings",
                "themes",
                "report_reasons",
                "report_statuses",
                "story_statuses",
                "story_relationship_types",
                "acknowledgment_roles",
                "tag_types"
            ]
        });
    }

    /// <summary>
    /// Wipes all application rows via FK-ordered deletes, restoring the DB to the migrated+lookup
    /// baseline. Called by <see cref="IntegrationTestBase.InitializeAsync"/> before each test.
    /// </summary>
    public async Task ResetAsync()
    {
        await using NpgsqlConnection conn = new(ConnectionString);
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[CollectionDefinition("Postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
