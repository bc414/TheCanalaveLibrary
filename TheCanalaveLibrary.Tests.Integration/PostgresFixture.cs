using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Starts one real, ephemeral Postgres container for the whole "Postgres" test collection (not per
/// test) — see testing.md "Integration tests run against real Postgres — never InMemory/SQLite."
/// Applies the actual <c>InitialSchema</c> migration once the container is up, proving the migration
/// itself applies cleanly against a real Postgres, independent of the full host.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("canalavedb_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

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
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[CollectionDefinition("Postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
