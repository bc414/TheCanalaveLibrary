using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="ProfileBanner"/> (WU30, Feature 21) covering the follow→vouch seam:
/// vouching is follow-gated, and <see cref="FollowButton"/> owns its own toggle (self-contained
/// write), so the banner has to hear about a follow made in the same visit or the vouch affordance
/// stays hidden until a reload — the staleness recorded in <c>audit/Following.md</c>.
///
/// <b>What is NOT tested here:</b> the vouch dialog's EditorView (Quill is JS-backed — manual band)
/// and service persistence (<c>FollowingWriteServiceTests</c>, Integration tier).
///
/// JSInterop is Loose so the vouch dialog's EditorView JS calls don't throw when it renders.
/// </summary>
public class ProfileBannerTests : BunitContext
{
    public ProfileBannerTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddScoped<IFollowingWriteService>(_ => new FakeFollowingWriteService());
    }

    private static ProfileHeaderDto Header(UserRelationshipStateDto? relationship) => new(
        UserId: 7,
        Username: "ReaderGamma",
        AvatarUrl: null,
        Tagline: null,
        Badges: [],
        OutgoingVouches: [],
        Stats: null,
        RelationshipState: relationship,
        ProfileVisibility: ProfileVisibility.Public,
        AllowProfileComments: SocialInteractionPermission.Public,
        ShowUserStats: false,
        LastSeenUtc: null,
        VerificationCode: null);

    private IRenderedComponent<ProfileBanner> RenderBanner(UserRelationshipStateDto relationship) =>
        Render<ProfileBanner>(p => p
            .Add(c => c.Header, Header(relationship))
            .Add(c => c.IsOwner, false)
            .Add(c => c.RelationshipState, relationship));

    [Fact]
    public void ProfileBanner_WhenNotFollowing_RendersNoVouchButton()
    {
        IRenderedComponent<ProfileBanner> cut = RenderBanner(
            new UserRelationshipStateDto(IsFollowing: false, ReceiveAlerts: false, IsVouched: false, OutgoingVouchCount: 0));

        cut.FindAll("button").Select(b => b.TextContent.Trim())
            .Should().NotContain("Vouch");
    }

    [Fact]
    public void ProfileBanner_WhenAlreadyFollowing_RendersVouchButton()
    {
        IRenderedComponent<ProfileBanner> cut = RenderBanner(
            new UserRelationshipStateDto(IsFollowing: true, ReceiveAlerts: true, IsVouched: false, OutgoingVouchCount: 0));

        cut.FindAll("button").Select(b => b.TextContent.Trim())
            .Should().Contain("Vouch");
    }

    [Fact]
    public void ProfileBanner_AfterFollowingInTheSameVisit_RevealsVouchButton_WithoutAReload()
    {
        IRenderedComponent<ProfileBanner> cut = RenderBanner(
            new UserRelationshipStateDto(IsFollowing: false, ReceiveAlerts: false, IsVouched: false, OutgoingVouchCount: 0));

        cut.FindAll("button").First(b => b.TextContent.Trim() == "Follow").Click();

        cut.FindAll("button").Select(b => b.TextContent.Trim())
            .Should().Contain("Vouch", "the follow just made in this visit gates the vouch affordance");
    }
}
