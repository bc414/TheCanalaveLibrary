using Bunit;
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
            LastSeenUtc: null);

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
        Services.AddScoped<IStoryReadService>(_ => new FakeStoryReadService());
        Services.AddScoped<IUserStoryInteractionReadService>(_ => new FakeInteractionReadService());
        Services.AddScoped<IRecommendationReadService>(_ => new FakeRecommendationReadService());
        Services.AddScoped<IBlogPostReadService>(_ => _blogService);
        Services.AddScoped<ISeriesReadService>(_ => new FakeSeriesReadService());
        Services.AddScoped<ISavedTagSelectionReadService>(_ => new FakeSavedTagSelectionReadService());
        Services.AddScoped<ISavedTagSelectionWriteService>(_ => new FakeSavedTagSelectionWriteService());
        // WU-CustomLists — ProfilePage now loads the Lists tab via ICustomListReadService.
        Services.AddScoped<ICustomListReadService>(_ => new FakeCustomListWriteService());
        Services.AddScoped<IToastService>(_ => new ToastService());
        Services.AddScoped<IDeviceDetectionService>(_ => new AlwaysDesktopDeviceService());
        // WU-Seo — ProfilePage now renders <SocialMetaTags>, which needs IPublicUrlProvider.
        // PublicUrlProvider is a pure Core class (no host dependency); a fixed test base is fine.
        Services.AddScoped<IPublicUrlProvider>(_ => new PublicUrlProvider("https://test.local"));

        // Anonymous AuthenticationState cascading value.
        var anonState = Task.FromResult(new AuthenticationState(new ClaimsPrincipal()));
        Services.AddCascadingValue(_ => anonState);
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
}
