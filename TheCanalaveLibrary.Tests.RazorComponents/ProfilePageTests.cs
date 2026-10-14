using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// F1-soundness tests for <see cref="ProfilePage"/> (WU-ComponentSoundness).
///
/// Verifies that in-place tab navigation (router-intercepted <c>&lt;a href&gt;</c>) correctly reloads
/// the tab payload on the same component instance. Pre-fix, the load was in <c>OnInitializedAsync</c>
/// only and did not re-fire on param change; post-fix it runs in <c>OnParametersSetAsync</c> with a
/// changed-tab guard.
///
/// The test observable: the bio text that <c>GetProfileTextAsync</c> returns for the Profile tab must
/// appear in the markup on Profile tab and be absent on Blog tab, and vice versa — if the lifecycle
/// fix did not fire on tab switch, the old tab's markup would linger or the new tab's content would
/// be missing.
///
/// <b>Services under test:</b> The lifecycle reload path in <see cref="ProfilePage"/> — specifically
/// that <c>OnParametersSetAsync</c> calls <c>LoadHeaderAsync</c> + <c>LoadTabPayloadAsync</c> when
/// the Tab route parameter changes.
///
/// <b>Not tested here:</b>  ChapterReadingPage F1 (JS-interop + multi-service; covered by manual
/// boot gate), Tailwind layout (human sign-off for Stage 6), ProfileBanner/FollowButton interactions
/// (see FollowButtonTests / VouchButtonTests).
///
/// <b>Tier:</b> RazorComponents (bUnit, no host or DB).
/// </summary>
public class ProfilePageTests : BunitContext
{
    private const int TestUserId = 1;
    private const string BioText = "This is the test bio.";
    private const string BlogPostTitle = "Blog Post Alpha";

    private readonly FakeUserProfileReadService _profileService;
    private readonly FakeBlogPostReadService _blogService;
    private readonly FakeStoryReadService _storyReadService = new();
    private readonly FakeUserSettingsService _settingsService = new();
    private readonly FakeInteractionReadService _interactionReadService = new();
    private readonly FakeReportSubmissionService _reports = new() { Reasons = [new ReportReasonDto(1, "Spam", null)] };

    // The viewer: anonymous unless a test calls SignInAs before rendering. AddAuthorization (not a
    // bare cascading value) because the story tabs' TagFilter renders an AuthorizeView.
    private readonly BunitAuthorizationContext _auth;

    private void SignInAs(int userId) =>
        _auth.SetAuthorized($"user-{userId}").SetClaims(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));

    public ProfilePageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Minimal public profile header. AllowProfileComments=Nobody keeps CommentSection
        // off the DOM for the Profile tab, so ICommentWriteService is not required here.
        var header = new ProfileHeaderDto(
            UserId: TestUserId,
            Username: "TestUser",
            AvatarUrl: null,
            Tagline: null,
            Badges: [],
            OutgoingVouches: [],
            Stats: null,
            RelationshipState: null,
            ProfileVisibility: ProfileVisibility.Public,
            AllowProfileComments: SocialInteractionPermission.Nobody,
            ShowUserStats: false,
            LastSeenUtc: null,
            VerificationCode: null);

        _profileService = new FakeUserProfileReadService(header) { BioHtml = BioText };
        _blogService = new FakeBlogPostReadService
        {
            AuthorResult = (
                [new BlogPostListingDto(1, BlogPostTitle, "Snippet", DateTime.UtcNow, Rating.T, false)],
                TotalCount: 1)
        };

        Services.AddScoped<IUserProfileReadService>(_ => _profileService);
        Services.AddScoped<IFollowingReadService>(_ => new FakeFollowingWriteService());
        Services.AddScoped<IFollowingWriteService>(_ => new FakeFollowingWriteService());
        Services.AddScoped<IStoryReadService>(_ => _storyReadService);
        Services.AddScoped<IUserStoryInteractionReadService>(_ => _interactionReadService);
        // A listed story's card nests UserStoryInteractionPanel (write service) and, for a signed-in
        // viewer, AddToCustomListMenu's inner half (the story-tab Report tests).
        Services.AddScoped<IUserStoryInteractionWriteService>(_ => new FakeUserStoryInteractionWriteService());
        Services.AddScoped<ICustomListWriteService>(_ => new FakeCustomListWriteService());
        Services.AddScoped<IRecommendationReadService>(_ => new FakeRecommendationReadService());
        Services.AddScoped<IBlogPostReadService>(_ => _blogService);
        Services.AddScoped<ISeriesReadService>(_ => new FakeSeriesReadService());
        Services.AddScoped<ISavedTagSelectionReadService>(_ => new FakeSavedTagSelectionReadService());
        Services.AddScoped<ISavedTagSelectionWriteService>(_ => new FakeSavedTagSelectionWriteService());
        // WU-CustomLists — ProfilePage now loads the Lists tab via ICustomListReadService.
        Services.AddScoped<ICustomListReadService>(_ => new FakeCustomListWriteService());
        Services.AddScoped<IToastService>(_ => new ToastService());
        // WU-UserModeration — the page's single ReportDialog moved out of the Stories tab to page
        // level (the banner's "Report user" control is on every tab), so it now renders always.
        Services.AddScoped<IReportSubmissionService>(_ => _reports);
        // WU-Seo — ProfilePage now renders <SocialMetaTags>, which needs IPublicUrlProvider.
        // PublicUrlProvider is a pure Core class (no host dependency); a fixed test base is fine.
        Services.AddScoped<IPublicUrlProvider>(_ => new PublicUrlProvider("https://test.local"));

        // The story tabs size their page from the viewer's DefaultPaginationSize (tracker B16).
        Services.AddScoped<IUserSettingsService>(_ => _settingsService);
        // The story tabs' ResultsFilterPanel: TagSelector/ShipFilter inject ITagReadService, TagChip
        // ISpriteReadService.
        Services.AddScoped<ITagReadService>(_ => new FakeTagReadService());
        Services.AddSingleton<ISpriteReadService>(new OptimisticSpriteReadService("/sprites/themes"));

        // AuthenticationState cascade: anonymous by default (see SignInAs).
        _auth = this.AddAuthorization();
    }

    // ── F1 lifecycle-reload test ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that switching the Tab route parameter on the same ProfilePage instance triggers a
    /// reload of the tab payload via OnParametersSetAsync (the F1 soundness fix).
    ///
    /// Pre-fix: load ran only in OnInitializedAsync; switching tabs via the router-intercepted
    /// &lt;a href&gt; strips did not re-run that method, so the profile-tab bio text would linger on
    /// the blog tab (stale content).
    ///
    /// Post-fix: OnParametersSetAsync detects a tab change, calls LoadTabPayloadAsync for the new
    /// tab, and the markup reflects the new tab's data.
    /// </summary>
    [Fact]
    public void TabSwitch_OnSameInstance_ReloadsTabPayload()
    {
        // Start on the Profile tab — bio text should be visible.
        IRenderedComponent<ProfilePage> cut = Render<ProfilePage>(p => p
            .Add(c => c.UserId, TestUserId)
            .Add(c => c.Tab, "profile"));

        cut.Markup.Should().Contain(BioText,
            "Profile tab bio text must appear on initial render");
        cut.Markup.Should().NotContain(BlogPostTitle,
            "Blog tab content must not appear on the Profile tab");

        // Simulate same-component tab navigation (router reuses the instance and calls
        // SetParametersAsync — which triggers OnParametersSetAsync with the new Tab value).
        cut.Render(p => p
            .Add(c => c.UserId, TestUserId)
            .Add(c => c.Tab, "blog"));

        // Post-fix: OnParametersSetAsync detects Tab changed → calls LoadBlogAsync → blog post appears.
        cut.Markup.Should().Contain(BlogPostTitle,
            "Blog tab title must appear after tab switch — OnParametersSetAsync reloaded the payload");
        cut.Markup.Should().NotContain(BioText,
            "Profile tab bio text must not appear on the Blog tab after reload");
    }

    // ── Story-tab page size (tracker B16) ────────────────────────────────────────────────────

    [Fact]
    public void SignedInViewer_StoryTabs_AreSizedByTheViewersDefaultPaginationSize()
    {
        SignInAs(99); // a visitor, not the profile's owner: the size is the VIEWER's preference
        _settingsService.DefaultPaginationSize = 50;

        Render<ProfilePage>(p => p
            .Add(c => c.UserId, TestUserId)
            .Add(c => c.Tab, ProfileTabSlug.For(ProfileTab.Favorites)));

        _storyReadService.LastListingsFilter!.PageSize.Should().Be(50);
    }

    [Fact]
    public void SignedInViewer_WhoseSettingsReadFails_StillGetsTheProfile_AtTwenty()
    {
        // A WASM tab whose cookie expired after prerender: the auth state still says signed in, the
        // settings read answers 401. Every other read on this public page degrades, so this one must
        // too (WU-TrackerFixes review fixes) — before, the exception reached the page's error boundary.
        SignInAs(99);
        _settingsService.GetMySettingsException = new SessionExpiredException();

        IRenderedComponent<ProfilePage> cut = Render<ProfilePage>(p => p
            .Add(c => c.UserId, TestUserId)
            .Add(c => c.Tab, ProfileTabSlug.For(ProfileTab.Favorites)));

        cut.Markup.Should().Contain("TestUser", "the profile renders");
        _storyReadService.LastListingsFilter!.PageSize.Should().Be(20, "a failed preference read keeps the default");
    }

    [Fact]
    public void AnonymousViewer_StoryTabs_KeepTwenty_WithoutReadingSettings()
    {
        _settingsService.DefaultPaginationSize = 50;

        Render<ProfilePage>(p => p
            .Add(c => c.UserId, TestUserId)
            .Add(c => c.Tab, ProfileTabSlug.For(ProfileTab.Favorites)));

        _storyReadService.LastListingsFilter!.PageSize.Should().Be(20);
        _settingsService.GetMySettingsCalls.Should().Be(0,
            "an anonymous viewer has no settings, and the WASM settings read requires auth");
    }

    // ── Blog-tab Report (tracker B17) ────────────────────────────────────────────────────────

    private IRenderedComponent<ProfilePage> RenderBlogTab() =>
        Render<ProfilePage>(p => p
            .Add(c => c.UserId, TestUserId)
            .Add(c => c.Tab, ProfileTabSlug.For(ProfileTab.Blog)));

    private static bool IsReportButton(AngleSharp.Dom.IElement b) => b.TextContent.Trim() == "Report";

    [Fact]
    public async Task BlogTab_SignedInVisitor_CanReportAPost_ThroughThePagesDialog()
    {
        SignInAs(99);

        IRenderedComponent<ProfilePage> cut = RenderBlogTab();
        cut.FindAll("button").Where(IsReportButton).Should().ContainSingle(
            "a signed-in visitor sees Report on each post card");

        cut.FindAll("button").First(IsReportButton).Click();

        cut.Find("[role='dialog']").GetAttribute("aria-label").Should().Be("Report Content",
            "the card's Report opens the page's single ReportDialog");

        // The page's dialog also serves the banner's "Report user": the card must file a BlogPost report.
        await ReportDialogDriver.SubmitAsync(cut);
        _reports.Submitted.Should().ContainSingle().Which
            .Should().Match<SubmitReportRequest>(r => r.EntityType == ReportedEntityType.BlogPost && r.EntityId == 1);
    }

    [Theory]
    [InlineData(false)] // anonymous: the report endpoint requires auth
    [InlineData(true)]  // the owner: never offered on their own posts
    public void BlogTab_NoReport_ForAnonymousViewers_OrTheOwner(bool signedInAsOwner)
    {
        if (signedInAsOwner) SignInAs(TestUserId);

        IRenderedComponent<ProfilePage> cut = RenderBlogTab();

        cut.Markup.Should().Contain(BlogPostTitle, "the post card itself still renders");
        cut.FindAll("button").Where(IsReportButton).Should().BeEmpty();
    }
    // ── Story tabs: Report and the interaction marks (WU-TrackerFixes browser pass, 2026-10-13) ────

    private IRenderedComponent<ProfilePage> RenderAuthoredTabWithOneStory()
    {
        _storyReadService.ListingsResult = ([new StoryListingDto(
            42, "Listed Story", "A description.", null, AuthorId: TestUserId, AuthorName: "TestUser",
            WordCount: 1000, StoryStatusId: StoryStatusEnum.InProgress, Rating: Rating.T,
            LastUpdatedDate: DateTime.UtcNow, Tags: [])], 1);
        return Render<ProfilePage>(p => p
            .Add(c => c.UserId, TestUserId)
            .Add(c => c.Tab, ProfileTabSlug.For(ProfileTab.Authored)));
    }

    [Theory]
    [InlineData(false, 0)] // anonymous: the report endpoint requires auth (layer2-services.md "Report entry points")
    [InlineData(true, 1)]  // a signed-in visitor
    public void StoryTab_CardReport_IsOfferedToSignedInViewersOnly(bool signedIn, int expected)
    {
        // Wired for everyone, an anonymous Report crashed the page on WASM (the reasons read 401'd) and
        // filed an anonymous report on the circuit.
        if (signedIn) SignInAs(99);

        IRenderedComponent<ProfilePage> cut = RenderAuthoredTabWithOneStory();
        cut.Find("button[aria-label='Story options']").Click();

        cut.FindAll("button").Count(IsReportButton).Should().Be(expected);
    }

    [Fact]
    public void StoryTab_InteractionMarksReadFails_TheStoriesStillRender()
    {
        // A WASM tab whose cookie expired after prerender: the marks read answers 401. It took the story
        // tab down through the page's error boundary; the cards now render unmarked.
        SignInAs(99);
        _interactionReadService.StatesException = new SessionExpiredException();

        IRenderedComponent<ProfilePage> cut = RenderAuthoredTabWithOneStory();

        cut.Markup.Should().Contain("Listed Story");
    }
}
