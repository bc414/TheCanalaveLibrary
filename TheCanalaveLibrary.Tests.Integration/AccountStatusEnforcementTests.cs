using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for WU38a's account-status login enforcement (folded in from the
/// previously-deferred follow-up noted in <c>workplan.md</c> after WU39 — see
/// <c>canalave-conventions/security.md</c> "Account-Status Enforcement").
///
/// <para><b>What's tested:</b>
/// <list type="bullet">
///   <item><see cref="CanalaveSignInManager.CanSignInAsync"/> blocks Banned and
///   currently-Suspended users, allows Active/Warned/expired-Suspended.</item>
///   <item>Resolving <c>SignInManager&lt;User&gt;</c> from the real DI container yields the
///   override (proves the <c>.AddSignInManager&lt;CanalaveSignInManager&gt;()</c> registration
///   actually wins over <c>AddApiEndpoints</c>'s default), and
///   <c>CheckPasswordSignInAsync</c> — the same path <c>Login.razor</c> calls — surfaces
///   <see cref="SignInResult.NotAllowed"/> for a Banned user without ever touching HttpContext
///   (this test calls <c>CheckPasswordSignInAsync</c>, not <c>PasswordSignInAsync</c>, precisely
///   because the latter's cookie-write step requires a live HttpContext that a plain DI scope
///   doesn't have — <c>PreSignInCheck</c>, where <c>CanSignInAsync</c> lives, is HttpContext-free).</item>
///   <item><see cref="ServerModerationWriteService.ApplyAccountActionAsync"/> bumps the security
///   stamp on Suspend/Ban (kills any already-open session via the existing 30-min revalidation)
///   but leaves it untouched on Warn.</item>
///   <item><see cref="ApplicationUserClaimsPrincipalFactory"/> bakes
///   <see cref="ActiveUserClaimTypes.AccountStatus"/> into the generated claims principal.</item>
/// </list>
/// </para>
///
/// <b>What stays manual</b> (per testing.md "What stays manual"): the real end-to-end cookie
/// write/expiry and the 30-minute <c>IdentityRevalidatingAuthenticationStateProvider</c> timing —
/// this suite proves the stamp changes, not the wall-clock revalidation.
///
/// Tier: <b>Integration</b> (real Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class AccountStatusEnforcementTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    // ── CanSignInAsync — direct ───────────────────────────────────────────────────

    [Fact]
    public async Task CanSignInAsync_ActiveUser_ReturnsTrue()
    {
        int userId = await SeedUserAsync();
        bool result = await CanSignInAsync(userId);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanSignInAsync_WarnedUser_ReturnsTrue()
    {
        int userId = await SeedUserAsync();
        await SetAccountStatusAsync(userId, AccountStatusEnum.Warned);

        bool result = await CanSignInAsync(userId);
        result.Should().BeTrue("a warning must not block sign-in — only a banner surfaces");
    }

    [Fact]
    public async Task CanSignInAsync_BannedUser_ReturnsFalse()
    {
        int userId = await SeedUserAsync();
        await SetAccountStatusAsync(userId, AccountStatusEnum.Banned);

        bool result = await CanSignInAsync(userId);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanSignInAsync_SuspendedUntilFuture_ReturnsFalse()
    {
        int userId = await SeedUserAsync();
        await SetAccountStatusAsync(userId, AccountStatusEnum.Suspended, DateTime.UtcNow.AddDays(7));

        bool result = await CanSignInAsync(userId);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanSignInAsync_SuspendedUntilPast_ReturnsTrue()
    {
        int userId = await SeedUserAsync();
        await SetAccountStatusAsync(userId, AccountStatusEnum.Suspended, DateTime.UtcNow.AddDays(-1));

        bool result = await CanSignInAsync(userId);
        result.Should().BeTrue("an expired suspension must no longer block sign-in");
    }

    // ── Wiring — SignInManager<User> resolves to the override, via the real Login.razor path ──

    [Fact]
    public async Task ResolvedSignInManager_IsCanalaveSignInManager()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        SignInManager<User> signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<User>>();

        signInManager.Should().BeOfType<CanalaveSignInManager>(
            "AddSignInManager<CanalaveSignInManager>() must be the last SignInManager<User> " +
            "registration so every sign-in path is covered");
    }

    [Fact]
    public async Task CheckPasswordSignInAsync_BannedUser_ReturnsNotAllowed()
    {
        int userId = await SeedUserAsync();
        await SetAccountStatusAsync(userId, AccountStatusEnum.Banned);

        using IServiceScope scope = Factory.Services.CreateScope();
        UserManager<User> userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        SignInManager<User> signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<User>>();
        User user = (await userManager.FindByIdAsync(userId.ToString()))!;

        SignInResult result = await signInManager.CheckPasswordSignInAsync(user, "Password123!", lockoutOnFailure: false);

        result.Should().Be(SignInResult.NotAllowed);
    }

    [Fact]
    public async Task CheckPasswordSignInAsync_ActiveUser_Succeeds()
    {
        int userId = await SeedUserAsync();

        using IServiceScope scope = Factory.Services.CreateScope();
        UserManager<User> userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        SignInManager<User> signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<User>>();
        User user = (await userManager.FindByIdAsync(userId.ToString()))!;

        SignInResult result = await signInManager.CheckPasswordSignInAsync(user, "Password123!", lockoutOnFailure: false);

        result.Succeeded.Should().BeTrue();
    }

    // ── Security-stamp bump on Suspend/Ban (kills open sessions), not on Warn ────────

    [Fact]
    public async Task ApplyAccountActionAsync_SuspendUser_BumpsSecurityStamp()
    {
        int modId = await SeedUserAsync("Moderator");
        int targetUserId = await SeedUserAsync("ToSuspend");
        long reportId = await SeedUserReportAsync(targetUserId, modId);

        string? stampBefore = await GetSecurityStampAsync(targetUserId);

        SetActiveUser(FakeActiveUserContext.Moderator(modId));
        await GetModWriteService().ApplyAccountActionAsync(
            reportId, ModeratorActionType.SuspendUser, "rule violation", DateTime.UtcNow.AddDays(7));

        string? stampAfter = await GetSecurityStampAsync(targetUserId);
        stampAfter.Should().NotBe(stampBefore,
            "suspending a user must invalidate any already-open session via the stamp revalidation");
    }

    [Fact]
    public async Task ApplyAccountActionAsync_BanUser_BumpsSecurityStamp()
    {
        int modId = await SeedUserAsync("Moderator");
        int targetUserId = await SeedUserAsync("ToBan");
        long reportId = await SeedUserReportAsync(targetUserId, modId);

        string? stampBefore = await GetSecurityStampAsync(targetUserId);

        SetActiveUser(FakeActiveUserContext.Moderator(modId));
        await GetModWriteService().ApplyAccountActionAsync(reportId, ModeratorActionType.BanUser, "severe violation");

        string? stampAfter = await GetSecurityStampAsync(targetUserId);
        stampAfter.Should().NotBe(stampBefore);
    }

    [Fact]
    public async Task ApplyAccountActionAsync_WarnUser_DoesNotBumpSecurityStamp()
    {
        int modId = await SeedUserAsync("Moderator");
        int targetUserId = await SeedUserAsync("ToWarn");
        long reportId = await SeedUserReportAsync(targetUserId, modId);

        string? stampBefore = await GetSecurityStampAsync(targetUserId);

        SetActiveUser(FakeActiveUserContext.Moderator(modId));
        await GetModWriteService().ApplyAccountActionAsync(reportId, ModeratorActionType.WarnUser, "minor issue");

        string? stampAfter = await GetSecurityStampAsync(targetUserId);
        stampAfter.Should().Be(stampBefore, "a warning must not log the user out of an open session");
    }

    // ── Claim baking ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ClaimsFactory_BakesAccountStatusClaim()
    {
        int userId = await SeedUserAsync();
        await SetAccountStatusAsync(userId, AccountStatusEnum.Warned);

        using IServiceScope scope = Factory.Services.CreateScope();
        UserManager<User> userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        IUserClaimsPrincipalFactory<User> claimsFactory =
            scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<User>>();
        User user = (await userManager.FindByIdAsync(userId.ToString()))!;

        ClaimsPrincipal principal = await claimsFactory.CreateAsync(user);

        principal.FindFirstValue(ActiveUserClaimTypes.AccountStatus).Should().Be(nameof(AccountStatusEnum.Warned));
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    private async Task<bool> CanSignInAsync(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        UserManager<User> userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        SignInManager<User> signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<User>>();
        User user = (await userManager.FindByIdAsync(userId.ToString()))!;
        return await signInManager.CanSignInAsync(user);
    }

    private async Task SetAccountStatusAsync(int userId, AccountStatusEnum status, DateTime? suspendedUntilUtc = null)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Users.Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.AccountStatus, status)
                .SetProperty(u => u.SuspendedUntilUtc, suspendedUntilUtc));
    }

    private async Task<string?> GetSecurityStampAsync(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Users.Where(u => u.Id == userId).Select(u => u.SecurityStamp).SingleAsync();
    }

    private IModerationWriteService GetModWriteService()
    {
        IServiceScope scope = Factory.Services.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<IModerationWriteService>();
    }

    private readonly List<IServiceScope> _scopes = [];

    public override async Task DisposeAsync()
    {
        foreach (IServiceScope scope in _scopes)
            scope.Dispose();
        await base.DisposeAsync();
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
