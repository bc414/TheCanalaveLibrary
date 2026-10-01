using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// The pure rule overloads of <see cref="ProfileVisibilityGuard"/> and
/// <see cref="BlogPostVisibilityGuard"/> (WU-AccessGateSweep2). Each guard's pure overload owns its
/// rule (<c>identity-and-authorization.md</c> §"Parent-visibility guards"), so the decision table is
/// testable without a database; the Integration tier (<c>ParentVisibilityContractTests</c>) covers
/// the projections that feed it.
/// <para>
/// The load-bearing case: a profile blog post is profile-tab data (Class A), so its author's
/// <see cref="ProfileVisibility"/> decides before the verified-bot elevation (Class B — consent
/// only) is consulted. Group and site posts carry no profile setting and are unaffected.
/// </para>
/// Tier: Unit.
/// </summary>
public class VisibilityGuardRuleTests
{
    private const int OwnerId = 10;
    private const int StrangerId = 20;

    private static StubActiveUserContext Anonymous() => new();
    private static StubActiveUserContext SignedIn(int id) => new() { UserId = id, IsAuthenticated = true };
    private static StubActiveUserContext Bot() => new() { IsVerifiedBot = true };

    // ── ProfileVisibilityGuard.IsVisible — the truth table ──────────────────────────────

    [Theory]
    [InlineData(ProfileVisibility.Public, true, true, true)]
    [InlineData(ProfileVisibility.UsersOnly, true, true, false)]
    [InlineData(ProfileVisibility.Private, true, false, false)]
    public void ProfileGuard_TruthTable(
        ProfileVisibility visibility, bool owner, bool signedInStranger, bool anonymous)
    {
        ProfileVisibilityGuard.IsVisible(visibility, SignedIn(OwnerId), OwnerId).Should().Be(owner);
        ProfileVisibilityGuard.IsVisible(visibility, SignedIn(StrangerId), OwnerId).Should().Be(signedInStranger);
        ProfileVisibilityGuard.IsVisible(visibility, Anonymous(), OwnerId).Should().Be(anonymous);
    }

    [Fact]
    public void ProfileGuard_MissingUser_Passes()
    {
        ProfileVisibilityGuard.IsVisible(null, Anonymous(), OwnerId).Should().BeTrue(
            "nothing to protect — the caller's own query returns empty for a missing user anyway");
    }

    [Fact]
    public void ProfileGuard_VerifiedBot_GetsNoElevation()
    {
        ProfileVisibilityGuard.IsVisible(ProfileVisibility.Private, Bot(), OwnerId).Should().BeFalse(
            "bots bypass consent (Class B), never privacy (Class A)");
    }

    // ── BlogPostVisibilityGuard.IsVisible — the profile-privacy half ────────────────────

    private static BlogPostVisibilityFacts ProfilePost(
        ProfileVisibility? authorVisibility, bool isPublished = true, Rating rating = Rating.E) =>
        new(BlogPostId: 1, AuthorId: OwnerId, IsPublished: isPublished, Rating: rating,
            IsGroupPost: false, GroupId: null, GroupAudience: null,
            AuthorProfileVisibility: authorVisibility);

    [Fact]
    public void BlogPost_PrivateAuthor_HiddenFromStrangerAndAnonymous()
    {
        BlogPostVisibilityFacts facts = ProfilePost(ProfileVisibility.Private);

        BlogPostVisibilityGuard.IsVisible(facts, SignedIn(StrangerId), isRevealed: false).Should().BeFalse();
        BlogPostVisibilityGuard.IsVisible(facts, Anonymous(), isRevealed: false).Should().BeFalse();
    }

    [Fact]
    public void BlogPost_PrivateAuthor_AuthorSeesOwnPost_PublishedOrDraft()
    {
        BlogPostVisibilityGuard.IsVisible(ProfilePost(ProfileVisibility.Private), SignedIn(OwnerId), false)
            .Should().BeTrue();
        BlogPostVisibilityGuard.IsVisible(ProfilePost(ProfileVisibility.Private, isPublished: false), SignedIn(OwnerId), false)
            .Should().BeTrue("authors keep their drafts");
    }

    [Fact]
    public void BlogPost_VerifiedBot_BypassesRating_ButNotProfilePrivacy()
    {
        BlogPostVisibilityGuard.IsVisible(ProfilePost(ProfileVisibility.Public, rating: Rating.M), Bot(), false)
            .Should().BeTrue("the bot elevation still bypasses consent");
        BlogPostVisibilityGuard.IsVisible(ProfilePost(ProfileVisibility.Private), Bot(), false)
            .Should().BeFalse("the profile check sits above the bot short-circuit");
    }

    [Fact]
    public void BlogPost_PrivateAuthor_RevealDoesNotUnlock()
    {
        BlogPostVisibilityGuard.IsVisible(ProfilePost(ProfileVisibility.Private, rating: Rating.M),
                SignedIn(StrangerId), isRevealed: true)
            .Should().BeFalse("a reveal is consent — it cannot override privacy");
    }

    [Fact]
    public void BlogPost_UsersOnlyAuthor_SignedInStrangerYes_AnonymousNo()
    {
        BlogPostVisibilityFacts facts = ProfilePost(ProfileVisibility.UsersOnly);

        BlogPostVisibilityGuard.IsVisible(facts, SignedIn(StrangerId), false).Should().BeTrue();
        BlogPostVisibilityGuard.IsVisible(facts, Anonymous(), false).Should().BeFalse();
    }

    [Fact]
    public void BlogPost_NullAuthorVisibility_GroupAndSitePosts_NoProfileCheck()
    {
        BlogPostVisibilityFacts groupPost = new(1, OwnerId, true, Rating.E, IsGroupPost: true, GroupId: 5,
            GroupAudience: Rating.E, AuthorProfileVisibility: null);
        BlogPostVisibilityFacts sitePost = new(2, OwnerId, true, Rating.E, IsGroupPost: false, GroupId: null,
            GroupAudience: null, AuthorProfileVisibility: null);

        BlogPostVisibilityGuard.IsVisible(groupPost, Anonymous(), false).Should().BeTrue(
            "a group post is the group's content, not its author's profile-tab data");
        BlogPostVisibilityGuard.IsVisible(sitePost, Anonymous(), false).Should().BeTrue(
            "a site announcement carries no profile setting");
    }

    [Fact]
    public void BlogPost_PublicAuthor_RatingGateUnchanged()
    {
        BlogPostVisibilityGuard.IsVisible(ProfilePost(ProfileVisibility.Public, rating: Rating.M),
                SignedIn(StrangerId), isRevealed: false)
            .Should().BeFalse("mature-off stranger, no reveal");
        BlogPostVisibilityGuard.IsVisible(ProfilePost(ProfileVisibility.Public, rating: Rating.M),
                SignedIn(StrangerId), isRevealed: true)
            .Should().BeTrue("a per-post reveal is the consent path");
    }
}
