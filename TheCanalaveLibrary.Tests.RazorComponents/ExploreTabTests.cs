using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="ExploreTab"/> (Feature 33 / WU40) — the build-your-own-map manual
/// tree search paradigm. Covers: direction-swapped toggle rows, the section model (compound rows,
/// per-section Show more), the deliberate Add-to-tree gesture growing the canvas, and toggle
/// changes re-querying with the right (edge, direction) flags. Tier: RazorComponents (bUnit;
/// the JS pan/persistence module is mocked loose — E2E covers gestures).
/// </summary>
public class ExploreTabTests : BunitContext
{
    private readonly FakeManualTreeSearchReadService _manualTree = new();

    public ExploreTabTests()
    {
        Services.AddScoped<IManualTreeSearchReadService>(_ => _manualTree);
        Services.AddScoped<IUserStoryInteractionReadService>(_ => new FakeInteractionReadService());
        Services.AddScoped<IUserStoryInteractionWriteService>(_ => new FakeUserStoryInteractionWriteService());
        Services.AddSingleton<ISpriteReadService>(new OptimisticSpriteReadService("/sprites/themes"));
        Services.AddScoped<ManualTreeStore>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static StoryListingDto MakeStory(int id, string? title = null) =>
        new(id, title ?? $"Story {id}", null, null, null, "Author", 5_000,
            StoryStatusEnum.InProgress, Rating.T, DateTime.UtcNow, []);

    private static UserCardDto MakeUser(int id, string? name = null) => new(id, name ?? $"User{id}", null, null, []);

    private static RecommendationDto MakeRec(int id, int storyId, UserCardDto recommender, bool gem = false, bool spotlight = false) =>
        new(id, storyId, recommender, "<p>rec body</p>", 3, gem, spotlight, 1, DateTime.UtcNow, false, false);

    private IRenderedComponent<ExploreTab> RenderStoryRoot()
    {
        IRenderedComponent<ExploreTab> cut = Render<ExploreTab>(p => p
            .Add(c => c.RootStory, MakeStory(1, "Root Story"))
            .Add(c => c.CurrentUserId, 99));
        cut.WaitForState(() => cut.FindComponents<ManualTreeCanvas>().Count > 0);
        return cut;
    }

    [Fact]
    public void StoryRoot_ShowsStoryDirectionToggles_NotUserDirection()
    {
        _manualTree.StoryResult = new ManualTreeNeighborsDto();

        IRenderedComponent<ExploreTab> cut = RenderStoryRoot();

        cut.WaitForAssertion(() =>
        {
            // Story anchor: story→user controls only — the whole row swaps per direction.
            cut.Markup.Should().Contain("Favorited by").And.Contain("Author Spotlight");
            cut.Markup.Should().NotContain("Vouch", "user→story controls never render for a story anchor");
        });
    }

    [Fact]
    public void StoryRoot_RendersAuthorSection_AndFavoriterCards()
    {
        _manualTree.StoryResult = new ManualTreeNeighborsDto
        {
            Author = MakeUser(10, "TheAuthor"),
            Favoriters = new ManualTreeSectionDto<UserCardDto>([MakeUser(20, "FavFan")], 1),
        };

        IRenderedComponent<ExploreTab> cut = RenderStoryRoot();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("TheAuthor").And.Contain("FavFan");
            cut.FindComponents<UserCard>().Should().HaveCount(2);
        });
    }

    [Fact]
    public void FamilySection_RendersCompoundRow_RecommendationCardPresent_BadgesStack()
    {
        UserCardDto rec = MakeUser(30, "GemSpotter");
        _manualTree.StoryResult = new ManualTreeNeighborsDto
        {
            RecommendationFamily = new ManualTreeSectionDto<ManualTreeRecItemDto>(
                [new ManualTreeRecItemDto(MakeRec(1, 1, rec, gem: true, spotlight: true), MakeStory(1, "Root Story"))], 1),
        };

        IRenderedComponent<ExploreTab> cut = RenderStoryRoot();

        cut.WaitForAssertion(() =>
        {
            // One row, both badges stacked on the ONE RecommendationCard — never split.
            cut.FindComponents<RecommendationCard>().Should().ContainSingle();
            cut.Markup.Should().Contain("Hidden Gem").And.Contain("Author's Pick");
        });
    }

    [Fact]
    public void ShowMore_RendersOnlyWhenMoreExist_WithHonestCount()
    {
        _manualTree.StoryResult = new ManualTreeNeighborsDto
        {
            Favoriters = new ManualTreeSectionDto<UserCardDto>([MakeUser(20)], 41),
        };

        IRenderedComponent<ExploreTab> cut = RenderStoryRoot();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Show more (40 more)"));
    }

    [Fact]
    public void AddToTree_GrowsTheCanvas_AsGhostNode()
    {
        _manualTree.StoryResult = new ManualTreeNeighborsDto { Author = MakeUser(10, "TheAuthor") };

        IRenderedComponent<ExploreTab> cut = RenderStoryRoot();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("+ Add to tree"));

        cut.FindAll("button").First(b => b.TextContent.Contains("+ Add to tree")).Click();

        cut.WaitForAssertion(() =>
        {
            cut.FindComponents<ManualTreeCanvas>().Single().FindAll("[data-tree-node]")
                .Should().HaveCount(2, "root + the added author node");
            cut.Markup.Should().Contain("✓ In tree", "the add button reflects per-node dedup");
        });
    }

    [Fact]
    public void TogglingAuthorOff_RequeriesWithIncludeAuthorFalse()
    {
        _manualTree.StoryResult = new ManualTreeNeighborsDto { Author = MakeUser(10) };

        IRenderedComponent<ExploreTab> cut = RenderStoryRoot();
        cut.WaitForAssertion(() => _manualTree.LastStoryRequest.Should().NotBeNull());
        _manualTree.LastStoryRequest!.IncludeAuthor.Should().BeTrue();

        cut.FindAll("input[type=checkbox]")[0].Change(false); // first pill = Author (story anchor)

        cut.WaitForAssertion(() => _manualTree.LastStoryRequest!.IncludeAuthor.Should().BeFalse(
            "the Author pair is independently toggleable — never hardcoded on"));
    }

    [Fact]
    public void UserRoot_ShowsUserDirectionToggles_AndPinnedBadgeOnAuthoredRow()
    {
        _manualTree.UserResult = new ManualTreeNeighborsDto
        {
            Authored = new ManualTreeSectionDto<StoryListingDto>(
                [MakeStory(5, "Pinned One"), MakeStory(6, "Other")], 2),
            PinnedStoryId = 5,
        };

        IRenderedComponent<ExploreTab> cut = Render<ExploreTab>(p => p
            .Add(c => c.RootUser, MakeUser(10, "RootUser"))
            .Add(c => c.CurrentUserId, 99));

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Vouch").And.Contain("Authored");
            cut.Markup.Should().NotContain("Favorited by", "story→user controls never render for a user anchor");
            cut.Markup.Should().Contain("📌 Pinned", "the pinned story is badged within Authored, not a separate section");
        });
    }
}
