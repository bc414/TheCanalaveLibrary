using System.Net;
using FluentAssertions;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Wire-level render guard for the app's STATIC-SSR pages — the Identity funnel and the
/// status-code page. These are the routes where <c>App.razor</c>'s
/// <c>AcceptsInteractiveRouting()</c> yields <c>null</c>, so the whole component tree (including
/// <c>MainLayout</c>'s chrome, reached via <c>AuthorizeRouteView</c>'s <c>DefaultLayout</c>)
/// renders with no render mode at all.
///
/// <b>Why this tier and not bUnit.</b> bUnit renders components with no render mode in every test,
/// so the RazorComponents tier cannot distinguish "renders fine statically" from "explodes when the
/// framework tries to infer a render mode for a persistence callback" — it never runs
/// <c>ComponentStatePersistenceManager.InferRenderModes</c> at all. A real
/// <see cref="TestAppFactory"/> request does. This class exists because that blind spot let tracker
/// item <b>H10</b> ship: the Global Flip (2026-07-13) put <c>[PersistentState]</c> on
/// <c>MessagesNavLink</c>, and every <c>/Account/*</c> page returned a raw 500 for 18 days with a
/// green suite the whole time. See <c>layer5-wasm.md</c> "Components that ALSO render on static-SSR
/// pages" for the rule these tests defend, and <c>scripts/check-render-modes.ps1</c> for the
/// static gate that catches the same class before it ever runs.
///
/// No seeding beyond the one authenticated case: these are plain anonymous GETs with no FK parents.
/// </summary>
[Collection("Postgres")]
public sealed class StaticSsrPageRenderTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    /// <summary>
    /// Every Identity page outside <c>Manage/</c> takes <c>AuthorizeRouteView</c>'s ambient
    /// <c>DefaultLayout="typeof(MainLayout)"</c> (none of them declares its own <c>@layout</c>), so
    /// they all carry the full SharedUI chrome under a null render mode. <c>Manage/</c> is
    /// deliberately absent — its <c>_Imports</c> pins <c>@layout ManageLayout</c>, which resolves to
    /// the Server-project <c>MainLayout</c> and reaches none of that chrome.
    /// </summary>
    [Theory]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Register")]
    [InlineData("/Account/ForgotPassword")]
    [InlineData("/Account/ResendEmailConfirmation")]
    [InlineData("/Account/Lockout")]
    [InlineData("/Account/InvalidUser")]
    public async Task IdentityPage_RendersForAnAnonymousVisitor(string path)
    {
        using HttpClient client = Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(path);
        string html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "static-SSR Identity pages must render without a render mode — a 500 here is the H10 class "
            + "(a persistence callback registered with no inferable render mode)");
        html.Should().Contain("<main", "the page must actually render its layout, not just return 200");
    }

    /// <summary>
    /// The signed-in case reaches strictly more chrome than the anonymous one:
    /// <c>NotificationBellInner</c> sits behind <c>NotificationBell</c>'s <c>AuthorizeView</c> and is
    /// only instantiated for an authenticated viewer, whereas <c>MessagesNavLink</c> keeps its
    /// <c>AuthorizeView</c> inside itself and is instantiated for everyone. Both carried the H10
    /// defect; only this test covers the former.
    /// </summary>
    [Fact]
    public async Task IdentityPage_RendersForASignedInVisitor()
    {
        int userId = await SeedUserAsync("static-ssr-viewer");
        SetActiveUser(userId);
        using HttpClient client = Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/Account/Login");
        string html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the authenticated tree adds NotificationBellInner to MainLayout's chrome");
        html.Should().Contain("<main");
    }

    /// <summary>
    /// <c>ContentGate/StatusCodePage.razor</c> is the other static-SSR page that opts into the
    /// SharedUI chrome — explicitly, via <c>@layout TheCanalaveLibrary.SharedUI.MainLayout</c>, while
    /// its folder <c>_Imports</c> carries <c>[ExcludeFromInteractiveRouting]</c>. It is reached two
    /// ways: re-executed by <c>UseStatusCodePagesWithReExecute</c> (which leaves the request
    /// interactive, so it survived H10), and by direct navigation to the URL (which did not — every
    /// <c>/status-code/*</c> URL returned 500 from 2026-07-24 until H10 was fixed). This asserts the
    /// direct path, since that is the one the re-execute path does not cover.
    ///
    /// The expectation is the requested code, not 200: the page sets the status code it renders.
    /// </summary>
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    public async Task StatusCodePage_RendersOnDirectNavigation(int code)
    {
        using HttpClient client = Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync($"/status-code/{code}");
        string html = await response.Content.ReadAsStringAsync();

        ((int)response.StatusCode).Should().Be(code,
            "the status-code page reports the code it was asked to render — a 500 means it failed to render at all");
        html.Should().Contain("<main");
    }
}
