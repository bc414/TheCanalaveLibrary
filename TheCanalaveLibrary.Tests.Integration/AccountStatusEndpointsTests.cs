using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="AccountStatusEndpoints"/> (WU-AccountEnforcement) — the
/// Layer-5 HTTP surface behind <see cref="IAccountStatusReadService"/>, exercised through
/// <c>Factory.CreateClient()</c> so the <c>.RequireAuthorization()</c> 401 floor and the real
/// routing/serialization both run. The assertion that matters most here is
/// <see cref="GetMyStatus_ReflectsALiveWriteThroughApplyAccountActionAsync"/>: it proves the read
/// is genuinely live off the DB row, not derived from any claim/cookie — the whole point of this
/// WU, since <c>AccountStatusBanner</c>'s claim-derived value was exactly what went stale.
/// Tier: Integration.
/// </summary>
[Collection("Postgres")]
public class AccountStatusEndpointsTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task GetMyStatus_Anonymous_Returns401()
    {
        // ResetSharedHostState already leaves the fake anonymous; explicit for clarity/intent.
        SetActiveUser(FakeActiveUserContext.Anonymous());

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/account-status");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMyStatus_ActiveUser_ReturnsActiveWithNullSuspendedUntil()
    {
        int userId = await SeedUserAsync();
        SetActiveUser(userId);

        HttpClient client = Factory.CreateClient();
        AccountStatusDto? dto = await client.GetFromJsonAsync<AccountStatusDto>("/api/account-status");

        dto.Should().NotBeNull();
        dto!.Status.Should().Be(AccountStatusEnum.Active);
        dto.SuspendedUntilUtc.Should().BeNull();
    }

    [Fact]
    public async Task GetMyStatus_SuspendedUser_CarriesSuspendedUntilUtc()
    {
        int userId = await SeedUserAsync();
        DateTime suspendedUntil = DateTime.UtcNow.AddDays(7);
        await SetAccountStatusAsync(userId, AccountStatusEnum.Suspended, suspendedUntil);
        SetActiveUser(userId);

        HttpClient client = Factory.CreateClient();
        AccountStatusDto? dto = await client.GetFromJsonAsync<AccountStatusDto>("/api/account-status");

        dto.Should().NotBeNull();
        dto!.Status.Should().Be(AccountStatusEnum.Suspended);
        dto.SuspendedUntilUtc.Should().BeCloseTo(suspendedUntil, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetMyStatus_ReflectsALiveWriteThroughApplyAccountActionAsync()
    {
        int modId = await SeedUserAsync("Moderator");
        int targetUserId = await SeedUserAsync("ToWarn");
        long reportId = await SeedUserReportAsync(targetUserId, modId);

        SetActiveUser(targetUserId);
        HttpClient client = Factory.CreateClient();
        AccountStatusDto? before = await client.GetFromJsonAsync<AccountStatusDto>("/api/account-status");
        before!.Status.Should().Be(AccountStatusEnum.Active, "the seeded user starts Active");

        // The real moderator write path — not a direct db.Users.ExecuteUpdateAsync shortcut — is
        // what proves this endpoint is live: nothing here refreshes a claim or a cookie.
        SetActiveUser(FakeActiveUserContext.Moderator(modId));
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            IModerationWriteService modWrite = scope.ServiceProvider.GetRequiredService<IModerationWriteService>();
            await modWrite.ApplyAccountActionAsync(reportId, ModeratorActionType.WarnUser, "minor issue");
        }

        SetActiveUser(targetUserId);
        AccountStatusDto? after = await client.GetFromJsonAsync<AccountStatusDto>("/api/account-status");
        after!.Status.Should().Be(AccountStatusEnum.Warned,
            "the same HttpClient, same endpoint, re-queried after a moderator action — a claim-derived read would still say Active");
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    private async Task SetAccountStatusAsync(int userId, AccountStatusEnum status, DateTime? suspendedUntilUtc = null)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Users.Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.AccountStatus, status)
                .SetProperty(u => u.SuspendedUntilUtc, suspendedUntilUtc));
    }

    /// <summary>Seeds a report targeting a User (required by ApplyAccountActionAsync).</summary>
    private async Task<long> SeedUserReportAsync(int targetUserId, int reporterId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        short reasonId = await db.ReportReasons.OrderBy(r => r.ReportReasonId)
            .Select(r => r.ReportReasonId).FirstAsync();

        Report report = new()
        {
            ReportedEntityType = ReportedEntityType.User,
            ReportedEntityId = targetUserId,
            ReportReasonId = reasonId,
            ReporterUserId = reporterId,
            ReportStatusId = ReportStatusEnum.Open,
            DateReported = DateTime.UtcNow,
        };
        db.Reports.Add(report);
        await db.SaveChangesAsync();
        return report.ReportId;
    }
}
