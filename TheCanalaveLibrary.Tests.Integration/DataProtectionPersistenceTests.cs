using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Verifies the WU-DataProtection wiring: the Data Protection key ring persists to the
/// <c>data_protection_keys</c> table (so auth cookies / antiforgery tokens survive process
/// replacement) rather than to the default ephemeral location. The cross-host test below is the
/// automated analog of the manual restart drill — a payload protected by one host must unprotect
/// on a fresh host sharing only the database. It also exercises the fresh-DI-scope
/// <c>ApplicationDbContext</c> resolution path the EF key repository uses at key-manager time
/// (outside any circuit/request). See security.md "Data Protection Keyring".
/// </summary>
[Collection("Postgres")]
public sealed class DataProtectionPersistenceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [Fact]
    public async Task ProtectUnprotect_RoundTrips_AndPersistsKeyRowToPostgres()
    {
        IDataProtectionProvider provider = Factory.Services.GetRequiredService<IDataProtectionProvider>();
        IDataProtector protector = provider.CreateProtector("Tests.DataProtectionPersistence");

        string protectedPayload = protector.Protect("canalave-round-trip");
        protector.Unprotect(protectedPayload).Should().Be("canalave-round-trip");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        int keyCount = await db.DataProtectionKeys.CountAsync();
        keyCount.Should().BeGreaterThan(0,
            "protecting a payload must have persisted at least one key to data_protection_keys");
    }

    [Fact]
    public void ProtectedPayload_SurvivesProcessReplacement_AcrossFactories()
    {
        // Host A protects…
        IDataProtector protectorA = Factory.Services
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("Tests.DataProtectionPersistence.CrossHost");
        string protectedPayload = protectorA.Protect("survives-redeploy");

        // …host A dies (deploy), host B boots against the same database…
        Factory.Dispose();
        using TestAppFactory secondFactory = new(_postgres.ConnectionString);
        IDataProtector protectorB = secondFactory.Services
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("Tests.DataProtectionPersistence.CrossHost");

        // …and can still read what A wrote. Without PersistKeysToDbContext this throws
        // CryptographicException (key not found) — the everyone-logged-out-on-deploy bug.
        protectorB.Unprotect(protectedPayload).Should().Be("survives-redeploy");
    }
}
