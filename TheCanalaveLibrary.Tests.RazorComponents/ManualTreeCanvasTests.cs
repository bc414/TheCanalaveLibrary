using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for the shared Manual Tree Search leaves (WU40): <see cref="ManualTreeCanvas"/>
/// (the 2D top-down node-link diagram both tabs share) and <see cref="ManualTreeEdgeToggles"/>
/// (the per-(edge, direction) pill row). Tier: RazorComponents (bUnit; pan/zoom gestures are
/// JS-module territory and are NOT testable here — E2E covers them).
/// </summary>
public class ManualTreeCanvasTests : BunitContext
{
    public ManualTreeCanvasTests()
    {
        Services.AddScoped<ManualTreeStore>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static (ManualTreeNode Root, Dictionary<(bool, int), ManualTreeNodeDisplay> Displays) MakeTree()
    {
        ManualTreeNode root = new() { EntityId = 1, IsStory = true };
        ManualTreeNode author = root.AddChild(false, 10, ManualTreeEdge.StoryAuthor, ghost: false);
        author.AddChild(true, 2, ManualTreeEdge.UserGemmedStory, ghost: true);
        Dictionary<(bool, int), ManualTreeNodeDisplay> displays = new()
        {
            [(true, 1)] = new("Root Story", null),
            [(false, 10)] = new("AuthorUser", null),
            [(true, 2)] = new("Gem Story", null),
        };
        return (root, displays);
    }

    private IRenderedComponent<ManualTreeCanvas> RenderCanvas(
        ManualTreeNode root, Dictionary<(bool, int), ManualTreeNodeDisplay> displays,
        string? selectedId = null, EventCallback<ManualTreeNode>? onClicked = null) =>
        Render<ManualTreeCanvas>(p =>
        {
            p.Add(c => c.Root, root)
             .Add(c => c.Displays, displays)
             .Add(c => c.SelectedNodeId, selectedId);
            if (onClicked is { } cb) p.Add(c => c.OnNodeClicked, cb);
        });

    [Fact]
    public void RendersOneChipPerNode_AndOneLinePerEdge()
    {
        var (root, displays) = MakeTree();

        IRenderedComponent<ManualTreeCanvas> cut = RenderCanvas(root, displays);

        cut.FindAll("[data-tree-node]").Should().HaveCount(3, "every node renders one chip");
        cut.FindAll("svg line").Should().HaveCount(2, "every non-root node has one connector line");
        cut.Markup.Should().Contain("Root Story").And.Contain("AuthorUser").And.Contain("Gem Story");
    }

    [Fact]
    public void EdgeLabels_RenderUnderChildChips()
    {
        var (root, displays) = MakeTree();

        IRenderedComponent<ManualTreeCanvas> cut = RenderCanvas(root, displays);

        cut.Markup.Should().Contain("Author").And.Contain("Hidden Gem");
    }

    [Fact]
    public void SelectedNode_CarriesAriaPressed_GhostNode_RendersDashed()
    {
        var (root, displays) = MakeTree();

        IRenderedComponent<ManualTreeCanvas> cut = RenderCanvas(root, displays, selectedId: root.NodeId);

        var chips = cut.FindAll("[data-tree-node]");
        chips.Count(c => c.GetAttribute("aria-pressed") == "true").Should().Be(1);
        chips.Count(c => c.ClassList.Contains("border-dashed")).Should().Be(1, "the ghost frontier node");
        cut.FindAll("svg line[stroke-dasharray]").Should().HaveCount(1, "the ghost edge is dashed too");
    }

    [Fact]
    public void ClickingChip_EmitsTheNode()
    {
        var (root, displays) = MakeTree();
        ManualTreeNode? clicked = null;

        IRenderedComponent<ManualTreeCanvas> cut = RenderCanvas(root, displays,
            onClicked: EventCallback.Factory.Create<ManualTreeNode>(this, n => clicked = n));

        cut.FindAll("[data-tree-node]")[0].Click();

        clicked.Should().NotBeNull();
        clicked!.EntityId.Should().Be(1);
    }
}

/// <summary>Toggle-pill row tests — every pill is one direction-specific pair.</summary>
public class ManualTreeEdgeTogglesTests : BunitContext
{
    [Fact]
    public void RendersOnePillPerItem_GroupedUnderLabels()
    {
        IRenderedComponent<ManualTreeEdgeToggles> cut = Render<ManualTreeEdgeToggles>(p => p
            .Add(c => c.Items, new List<ManualTreeEdgeToggles.EdgeToggleItem>
            {
                new("a", "Author", true),
                new("g", "Hidden Gem", true, "Family"),
                new("s", "Author Spotlight", false, "Family"),
            }));

        cut.FindAll("input[type=checkbox]").Should().HaveCount(3);
        cut.Markup.Should().Contain("Family");
        cut.FindAll("input[type=checkbox]")[2].HasAttribute("checked").Should().BeFalse();
    }

    [Fact]
    public void Toggling_EmitsKeyAndNewState()
    {
        (string Key, bool IsOn)? emitted = null;
        IRenderedComponent<ManualTreeEdgeToggles> cut = Render<ManualTreeEdgeToggles>(p => p
            .Add(c => c.Items, new List<ManualTreeEdgeToggles.EdgeToggleItem> { new("gem", "Hidden Gem", true) })
            .Add(c => c.OnToggled, EventCallback.Factory.Create<(string, bool)>(this, t => emitted = t)));

        cut.Find("input[type=checkbox]").Change(false);

        emitted.Should().NotBeNull();
        emitted!.Value.Key.Should().Be("gem");
        emitted.Value.IsOn.Should().BeFalse();
    }
}
