using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="AccountStatusBanner"/> (WU38a — Indicator-role element reading the
/// <c>canalave:account_status</c> claim baked at sign-in by
/// <c>ApplicationUserClaimsPrincipalFactory</c>). Covers: no render when anonymous, no render for
/// Active, renders the warning for Warned. Suspended/Banned never reach a live circuit — either
/// blocked at sign-in (<c>CanalaveSignInManager</c>) or ejected from an open session by the
/// security-stamp bump — so those statuses aren't exercised here (integration-tier, see
/// <c>AccountStatusEnforcementTests</c>).
/// Tier: RazorComponents (bUnit).
/// </summary>
public class AccountStatusBannerTests : BunitContext
{
    private readonly BunitAuthorizationContext _auth;

    public AccountStatusBannerTests()
    {
        _auth = this.AddAuthorization(); // anonymous/not-authorized by default
    }

    [Fact]
    public void Anonymous_RendersNothing()
    {
        IRenderedComponent<AccountStatusBanner> cut = Render<AccountStatusBanner>();

        cut.Markup.Should().BeEmpty();
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
        // WU) must not render — absence is treated the same as Active, never as a fail-open warning.
        _auth.SetAuthorized("no-claim-user");

        IRenderedComponent<AccountStatusBanner> cut = Render<AccountStatusBanner>();

        cut.Markup.Should().BeEmpty();
    }

    [Fact]
    public void WarnedAccount_RendersWarningBanner()
    {
        _auth.SetAuthorized("warned-user").SetClaims(
            new Claim(ActiveUserClaimTypes.AccountStatus, nameof(AccountStatusEnum.Warned)));

        IRenderedComponent<AccountStatusBanner> cut = Render<AccountStatusBanner>();

        cut.Find("[role='alert']").TextContent.Should().Contain("warning");
    }
}
