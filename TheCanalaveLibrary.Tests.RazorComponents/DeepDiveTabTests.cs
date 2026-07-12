using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="DeepDiveTab"/> (Feature 33 / WU40) — the bounded chain-of-trust
/// paradigm. Covers: click-is-the-only-gesture auto-add, the anti-bounce guard, the four
/// direction-labeled toggles gating future walks, and the floating info panel (non-blocking —
/// no backdrop). Tier: RazorComponents (bUnit; panel drag/resize is JS-module territory — E2E).
/// </summary>
public class DeepDiveTabTests : BunitContext
{
    private readonly FakeManualTreeSearchReadService _manualTree = new();

    public DeepDiveTabTests()
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

    [Fact]
    public void RootStory_AutoOpensOnLoad_AddingAuthorAndSpotlightedRecommenders()
    {
        // Clicking (here: the automatic root open) is the ONLY gesture — bounded connections
        // are added in the same action, no separate "Explore more" button.
        _manualTree.StoryResult = new ManualTreeNeighborsDto
        {
            Author = MakeUser(10, "ChainAuthor"),
            RecommendationFamily = new ManualTreeSectionDto<ManualTreeRecItemDto>(
                [new ManualTreeRecItemDto(MakeRec(1, 1, MakeUser(20, "Spotlighted"), spotlight: true), MakeStory(1))], 1),
        };

        IRenderedComponent<DeepDiveTab> cut = Render<DeepDiveTab>(p => p
            .Add(c => c.RootStory, MakeStory(1, "Root Story")));

        cut.WaitForAssertion(() =>
        {
            cut.FindComponents<ManualTreeCanvas>().Single().FindAll("[data-tree-node]")
                .Should().HaveCount(3, "root + author + spotlighted recommender, all auto-added");
            cut.Markup.Should().NotContain("Explore more", "no separate bulk-add button exists");
        });
    }

    [Fact]
    public void FourDirectionLabeledToggles_AllRenderAtOnce()
    {
        IRenderedComponent<DeepDiveTab> cut = Render<DeepDiveTab>(p => p
            .Add(c => c.RootStory, MakeStory(1)));

        cut.WaitForAssertion(() =>
        {
            // All four whitelisted pairs at once, direction in every label — the next click can
            // land on either node type (unlike Explore's per-anchor swap).
            cut.Markup.Should().Contain("Author (story → author)")
                .And.Contain("Hidden Gem (user → story)")
                .And.Contain("Author Spotlight (story → user)")
                .And.Contain("Pinned (user → story)");
            cut.FindAll("input[type=checkbox]").Should().HaveCount(4);
        });
    }

    [Fact]
    public void AuthorToggleOff_FutureClicksSkipTheAuthorEdge()
    {
        _manualTree.StoryResult = new ManualTreeNeighborsDto { Author = MakeUser(10) };

        IRenderedComponent<DeepDiveTab> cut = Render<DeepDiveTab>(p => p
            .Add(c => c.RootStory, MakeStory(1)));
        cut.WaitForAssertion(() => _manualTree.LastStoryRequest.Should().NotBeNull());

        cut.FindAll("input[type=checkbox]")[0].Change(false); // Author pill
        cut.FindComponents<ManualTreeCanvas>().Single().FindAll("[data-tree-node]")[0].Click(); // re-click root

        cut.WaitForAssertion(() => _manualTree.LastStoryRequest!.IncludeAuthor.Should().BeFalse(
            "the Author edge is independently toggleable — the Author×Pinned bounce fix"));
    }

    [Fact]
    public void AntiBounceGuard_NeverAutoAddsTheParentEntityBack()
    {
        // Story 1 authored by user 10; user 10's pinned story is story 1 — the identity
        // round-trip the guard exists for. Walk: root(1) auto-adds author(10); clicking
        // author(10) must NOT re-add story 1 beneath it.
        _manualTree.StoryResult = new ManualTreeNeighborsDto { Author = MakeUser(10, "SelfPinned") };
        _manualTree.UserResult = new ManualTreeNeighborsDto
        {
            Authored = new ManualTreeSectionDto<StoryListingDto>([MakeStory(1, "Root Story")], 1),
            PinnedStoryId = 1,
        };

        IRenderedComponent<DeepDiveTab> cut = Render<DeepDiveTab>(p => p
            .Add(c => c.RootStory, MakeStory(1, "Root Story")));
        cut.WaitForAssertion(() =>
            cut.FindComponents<ManualTreeCanvas>().Single().FindAll("[data-tree-node]").Should().HaveCount(2));

        // Click the author node (the non-root chip).
        cut.FindComponents<ManualTreeCanvas>().Single().FindAll("[data-tree-node]")[1].Click();

        cut.WaitForAssertion(() =>
            cut.FindComponents<ManualTreeCanvas>().Single().FindAll("[data-tree-node]")
                .Should().HaveCount(2, "the pinned story IS the parent — the A→B→A ring is guarded"));
    }

    [Fact]
    public void ClickingNode_OpensNonBlockingFloatingPanel_WithSkippedGroupsNote()
    {
        _manualTree.StoryResult = new ManualTreeNeighborsDto { Author = MakeUser(10, "ChainAuthor") };

        IRenderedComponent<DeepDiveTab> cut = Render<DeepDiveTab>(p => p
            .Add(c => c.RootStory, MakeStory(1, "Root Story")));

        cut.WaitForAssertion(() =>
        {
            // The root auto-open selects the root → the panel is already up.
            cut.Markup.Should().Contain("Root", "the panel header names the selection provenance");
            cut.FindAll(".fixed.inset-0").Should().BeEmpty("the panel is floating and NON-blocking — no backdrop");
            cut.FindComponents<StoryCard>().Should().NotBeEmpty("the panel shows the composed StoryCard");
        });

        // Close the panel; the canvas stays.
        cut.Find("button[aria-label='Close panel']").Click();
        cut.WaitForAssertion(() =>
        {
            cut.FindAll("button[aria-label='Close panel']").Should().BeEmpty();
            cut.FindComponents<ManualTreeCanvas>().Should().ContainSingle();
        });
    }

    [Fact]
    public void GemNode_PanelShowsTheRecommendationThatEarnedTheEdge()
    {
        UserCardDto gemmer = MakeUser(10, "Gemmer");
        _manualTree.StoryResult = new ManualTreeNeighborsDto { Author = gemmer };
        _manualTree.UserResult = new ManualTreeNeighborsDto
        {
            RecommendationFamily = new ManualTreeSectionDto<ManualTreeRecItemDto>(
                [new ManualTreeRecItemDto(MakeRec(7, 5, gemmer, gem: true), MakeStory(5, "The Gem"))], 1),
        };

        IRenderedComponent<DeepDiveTab> cut = Render<DeepDiveTab>(p => p
            .Add(c => c.RootStory, MakeStory(1, "Root Story")));
        cut.WaitForAssertion(() =>
            cut.FindComponents<ManualTreeCanvas>().Single().FindAll("[data-tree-node]").Should().HaveCount(2));

        // Walk: author → their gem story appears; then click the gem node.
        cut.FindComponents<ManualTreeCanvas>().Single().FindAll("[data-tree-node]")[1].Click();
        cut.WaitForAssertion(() =>
            cut.FindComponents<ManualTreeCanvas>().Single().FindAll("[data-tree-node]").Should().HaveCount(3));
        var chips = cut.FindComponents<ManualTreeCanvas>().Single().FindAll("[data-tree-node]");
        chips[2].Click();

        cut.WaitForAssertion(() =>
        {
            cut.FindComponents<RecommendationCard>().Should().ContainSingle(
                "a gem/spotlight node's panel shows the recommendation that earned it the edge");
            cut.Markup.Should().Contain("Reached via Hidden Gem");
        });
    }
}
