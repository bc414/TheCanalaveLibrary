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
    /// The account-deletion page is the compliance surface for what survives a deletion (worksheet
    /// D13), so its copy must not promise a "[Deleted Comment]" placeholder — owner ruling D12 declined
    /// it, and account deletion anonymizes comments to "[deleted user]" (WU-TptHardDelete review fixes).
    /// </summary>
    [Fact]
    public async Task DeletePersonalDataPage_DescribesCommentsAsAnonymized_NotAPlaceholder()
    {
        int userId = await SeedUserAsync("deletion-copy-viewer");
        SetActiveUser(userId);
        using HttpClient client = Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/Account/Manage/DeletePersonalData");
        string html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("[deleted user]");
        html.Should().NotContain("[Deleted Comment]", "D12 declined the placeholder; it never existed");
    }

    /// <summary>
    /// Owner ruling D13 (WU-UserDeletion): the page is the Art. 13/14 compliance surface, so it must
    /// enumerate the whole survival set (poll votes and sent messages included), the destroyed set, the
    /// delete-it-yourself-first paths (and where the UI closes them: a closed poll's vote, a recommendation
    /// the author removed), that sent messages cannot be deleted, that server logs are not erased, and that
    /// removing a name does not remove identifying details. A policy change that forgets the copy fails here.
    /// </summary>
    [Fact]
    public async Task DeletePersonalDataPage_EnumeratesTheSurvivalSet_AndTheDeleteFirstPaths()
    {
        int userId = await SeedUserAsync("deletion-policy-viewer");
        SetActiveUser(userId);
        using HttpClient client = Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/Account/Manage/DeletePersonalData");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Kept, without your name");
        html.Should().Contain("Your votes on polls", "poll votes survive anonymized (D13's correction)");
        html.Should().Contain("Private messages you sent. The other person keeps the conversation");
        html.Should().Contain("Groups you founded");
        html.Should().Contain("Permanently deleted");
        html.Should().Contain("linked sign-in providers", "account-level fields go unconditionally (D13 (iii))");
        html.Should().Contain("Comments other people left on your profile");
        html.Should().Contain("Delete it first");
        html.Should().Contain("Private messages you sent can't be deleted");
        html.Should().Contain("Removing your name does not remove what you wrote");
        html.Should().Contain("no admin until a site moderator assigns one", "D13 edge (b), the sole-admin consequence");
        html.Should().Contain($"href=\"/user/{userId}/series\"");

        // Review fixes: each delete-it-first path is stated as far as the UI offers it, and the page does not
        // claim more than the database delivers (D13 (iii)'s log half is a Phase-7 obligation, tracker E11).
        html.Should().Contain("Retract vote on a poll that is still open", "Retract vote exists only on an open poll");
        html.Should().Contain("Votes on a closed poll stay in its results, without your name");
        html.Should().Contain("A recommendation the story's author removed can't be deleted",
            "a Rejected recommendation is the sticky block record; it offers no Delete");
        html.Should().Contain("server logs record activity by account number");
        html.Should().Contain("They are not erased when your account is deleted");
        html.Should().NotContain("nothing links what remains back to you", "server logs can, until Phase 7 bounds them");

        // WU-BlobCleanup (owner ruling D14): the avatar's file goes with the account; covers stay with the
        // surviving stories (decision row 25).
        html.Should().Contain("Your profile, including your profile picture");
        html.Should().Contain("Stories (with their cover images)");
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
