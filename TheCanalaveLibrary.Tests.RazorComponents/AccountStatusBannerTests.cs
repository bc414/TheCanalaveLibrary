using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="AccountStatusBanner"/> (WU38a + WU-AccountEnforcement). First-paint
/// cases still come off the <c>canalave:account_status</c> claim (no query on initial render — see
/// the component's own header comment); the navigation-triggered cases pin the actual fix this WU
/// shipped: a live <see cref="IAccountStatusReadService"/> re-read on
/// <see cref="NavigationManager.LocationChanged"/>, following the <c>MessagesNavLink</c> pattern.
/// Tier: RazorComponents (bUnit).
/// </summary>
public class AccountStatusBannerTests : BunitContext
{
    private readonly BunitAuthorizationContext _auth;
    private readonly FakeAccountStatusReadService _statusService = new();

    public AccountStatusBannerTests()
    {
        _auth = this.AddAuthorization(); // anonymous/not-authorized by default
        Services.AddSingleton<IAccountStatusReadService>(_statusService);
        Services.AddLogging();
    }

    private void Navigate() =>
        Services.GetRequiredService<NavigationManager>().NavigateTo("/somewhere-else");

    // ── First paint (claim-only, no query) ──────────────────────────────────────

    [Fact]
    public void Anonymous_RendersNothing()
    {
        IRenderedComponent<AccountStatusBanner> cut = Render<AccountStatusBanner>();

        cut.Markup.Should().BeEmpty();
    }

    [Fact]
    public void Anonymous_Navigates_StillNeverCallsTheService()
    {
        Render<AccountStatusBanner>();

        Navigate();

        _statusService.CallCount.Should().Be(0,
            "an anonymous viewer never renders AccountStatusBanner's Authorized branch, so the " +
            "LocationChanged handler must short-circuit before querying");
    }

    [Fact]
    public void ActiveAccount_RendersNothing()
    {
        _auth.SetAuthorized("active-user").SetClaims(
            new Claim(ActiveUserClaimTypes.AccountStatus, nameof(AccountStatusEnum.Active)));

        IRenderedComponent<AccountStatusBanner> cut = Render<AccountStatusBanner>();

        cut.Markup.Should().BeEmpty();
    }

    [Fact]
    public void NoAccountStatusClaim_RendersNothing()
    {
        // An authenticated principal without the claim at all (e.g. stale cookie predating this
        // claim) must not render — absence is treated the same as Active, never as a fail-open warning.
        _auth.SetAuthorized("no-claim-user");

        IRenderedComponent<AccountStatusBanner> cut = Render<AccountStatusBanner>();

        cut.Markup.Should().BeEmpty();
    }

    [Fact]
    public void WarnedAccount_FirstPaintFromClaim_RendersWarningBannerWithNoQuery()
    {
        _auth.SetAuthorized("warned-user").SetClaims(
            new Claim(ActiveUserClaimTypes.AccountStatus, nameof(AccountStatusEnum.Warned)));

        IRenderedComponent<AccountStatusBanner> cut = Render<AccountStatusBanner>();

        cut.Find("[role='alert']").TextContent.Should().Contain("warning");
        _statusService.CallCount.Should().Be(0, "first paint uses the claim, not a live query");
    }

    // ── Navigation-triggered live read (the fix) ──────────────────────────────────

    [Fact]
    public void Navigation_WarnedFromLiveRead_RendersWarningBanner()
    {
        _auth.SetAuthorized("user").SetClaims(
            new Claim(ActiveUserClaimTypes.AccountStatus, nameof(AccountStatusEnum.Active)));
        _statusService.Status = AccountStatusEnum.Warned;
        IRenderedComponent<AccountStatusBanner> cut = Render<AccountStatusBanner>();
        cut.Markup.Should().BeEmpty("claim still says Active before the first navigation");

        Navigate();

        cut.Find("[role='alert']").TextContent.Should().Contain("warning");
        cut.Markup.Should().NotContain("Log out", "a warning must not eject the user — no sign-out affordance");
    }

    [Fact]
    public void Navigation_SuspendedFromLiveRead_RendersSuspendedCopyWithDateAndLogOut()
    {
        // A claim can never say Suspended — CanalaveSignInManager blocks that user at sign-in —
        // so this state is reachable ONLY via the live read, exactly the gap this WU closes.
        _auth.SetAuthorized("user").SetClaims(
            new Claim(ActiveUserClaimTypes.AccountStatus, nameof(AccountStatusEnum.Active)));
        DateTime suspendedUntil = new(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc);
        _statusService.Status = AccountStatusEnum.Suspended;
        _statusService.SuspendedUntilUtc = suspendedUntil;
        IRenderedComponent<AccountStatusBanner> cut = Render<AccountStatusBanner>();

        Navigate();

        string alert = cut.Find("[role='alert']").TextContent;
        alert.Should().Contain("suspended until");
        alert.Should().Contain(suspendedUntil.ToString("d"),
            "the copy must match Login.razor's BuildAccountStatusMessageAsync verbatim, including the {0:d} date format");
        cut.Markup.Should().Contain("Log out");
    }

    [Fact]
    public void Navigation_BannedFromLiveRead_RendersBannedCopyWithLogOut()
    {
        _auth.SetAuthorized("user").SetClaims(
            new Claim(ActiveUserClaimTypes.AccountStatus, nameof(AccountStatusEnum.Active)));
        _statusService.Status = AccountStatusEnum.Banned;
        IRenderedComponent<AccountStatusBanner> cut = Render<AccountStatusBanner>();

        Navigate();

        cut.Find("[role='alert']").TextContent.Should().Contain("permanently banned");
        cut.Markup.Should().Contain("Log out");
    }

    [Fact]
    public void Navigation_ActiveFromLiveRead_RemovesAnEarlierWarnedBanner()
    {
        // The no-un-warn-path caveat is a product fact, not a component constraint — the banner
        // itself must still react correctly if status ever does move back to Active.
        _auth.SetAuthorized("user").SetClaims(
            new Claim(ActiveUserClaimTypes.AccountStatus, nameof(AccountStatusEnum.Warned)));
        IRenderedComponent<AccountStatusBanner> cut = Render<AccountStatusBanner>();
        cut.Markup.Should().NotBeEmpty("first paint still shows the Warned claim");

        _statusService.Status = AccountStatusEnum.Active;
        Navigate();

        cut.Markup.Should().BeEmpty();
    }

    [Fact]
    public void Navigation_ServiceThrows_DegradesToLastKnownStatus()
    {
        _auth.SetAuthorized("user").SetClaims(
            new Claim(ActiveUserClaimTypes.AccountStatus, nameof(AccountStatusEnum.Warned)));
        _statusService.ThrowOnNextCall = true;
        IRenderedComponent<AccountStatusBanner> cut = Render<AccountStatusBanner>();

        Navigate();

        // Degraded but continuing (logging.md §"No silent catches" — the throw is still logged;
        // this test only pins the user-visible fallback behavior).
        cut.Find("[role='alert']").TextContent.Should().Contain("warning",
            "a failed refresh must keep showing the last-known status, not silently clear the banner");
    }
}
