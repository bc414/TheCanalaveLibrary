using AngleSharp.Dom;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="AddToCustomListMenu"/> (Feature 51, WU-CustomLists) — the StoryCard
/// caret-menu expander. Covers: hidden for anonymous viewers, on-demand membership load on expand,
/// toggle rows calling Add/Remove with the right (listId, storyId), the create-and-add two-step,
/// and validation errors surfaced via <c>InlineAlert</c>.
/// Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class AddToCustomListMenuTests : BunitContext
{
    private const int StoryId = 42;

    private readonly FakeCustomListWriteService _service = new();
    private readonly BunitAuthorizationContext _auth;

    public AddToCustomListMenuTests()
    {
        Services.AddScoped<ICustomListWriteService>(_ => _service);
        _auth = this.AddAuthorization(); // anonymous by default
    }

    private IRenderedComponent<AddToCustomListMenu> RenderMenu() =>
        Render<AddToCustomListMenu>(p => p.Add(c => c.StoryId, StoryId));

    private static Task ExpandAsync(IRenderedComponent<AddToCustomListMenu> cut) =>
        cut.Find("button[aria-label='Add to list']").ClickAsync(new MouseEventArgs());

    [Fact]
    public void Anonymous_RendersNothing()
    {
        IRenderedComponent<AddToCustomListMenu> cut = RenderMenu();
        cut.Markup.Should().BeEmpty();
    }

    [Fact]
    public async Task Expand_LoadsMembershipsOnDemand_AndRendersRows()
    {
        _auth.SetAuthorized("user");
        _service.Memberships =
        [
            new CustomListMembershipDto(1, "Comfort re-reads", true),
            new CustomListMembershipDto(2, "Worldbuilding", false),
        ];
        IRenderedComponent<AddToCustomListMenu> cut = RenderMenu();

        await ExpandAsync(cut);

        cut.Markup.Should().Contain("Comfort re-reads");
        cut.Markup.Should().Contain("Worldbuilding");
    }

    [Fact]
    public async Task ToggleRow_StoryNotInList_CallsAddWithListAndStoryIds()
    {
        _auth.SetAuthorized("user");
        _service.Memberships = [new CustomListMembershipDto(7, "Shelf", false)];
        IRenderedComponent<AddToCustomListMenu> cut = RenderMenu();
        await ExpandAsync(cut);

        await cut.Find("button[aria-label='Toggle list Shelf']").ClickAsync(new MouseEventArgs());

        _service.LastAddCall.Should().Be((7, StoryId));
        _service.LastRemoveCall.Should().BeNull();
    }

    [Fact]
    public async Task ToggleRow_StoryAlreadyInList_CallsRemove_ThenSecondClickAdds()
    {
        _auth.SetAuthorized("user");
        _service.Memberships = [new CustomListMembershipDto(7, "Shelf", true)];
        IRenderedComponent<AddToCustomListMenu> cut = RenderMenu();
        await ExpandAsync(cut);

        IElement row = cut.Find("button[aria-label='Toggle list Shelf']");
        await row.ClickAsync(new MouseEventArgs());
        _service.LastRemoveCall.Should().Be((7, StoryId));

        // Local state flipped — the same row now adds.
        await cut.Find("button[aria-label='Toggle list Shelf']").ClickAsync(new MouseEventArgs());
        _service.LastAddCall.Should().Be((7, StoryId));
    }

    [Fact]
    public async Task CreateAndAdd_CallsCreatePrivateThenAddsStory()
    {
        _auth.SetAuthorized("user");
        _service.NextId = 55;
        IRenderedComponent<AddToCustomListMenu> cut = RenderMenu();
        await ExpandAsync(cut);

        await cut.Find("button[aria-label='New list']").ClickAsync(new MouseEventArgs());
        await cut.Find("input[aria-label='New list name']")
            .InputAsync(new ChangeEventArgs { Value = "Sinnoh starter pack" });
        await cut.Find("button[aria-label='Create list and add story']").ClickAsync(new MouseEventArgs());

        _service.LastCreateCall.Should().Be(("Sinnoh starter pack", false)); // new lists default private
        _service.LastAddCall.Should().Be((55, StoryId));
        cut.Markup.Should().Contain("Sinnoh starter pack"); // appears as a checked row
    }

    [Fact]
    public async Task CreateAndAdd_ValidationException_ShowsErrorViaInlineAlert()
    {
        _auth.SetAuthorized("user");
        _service.ThrowOnCreate =
            new CustomListValidationException(["You already have a list named \"Dup\"."]);
        IRenderedComponent<AddToCustomListMenu> cut = RenderMenu();
        await ExpandAsync(cut);

        await cut.Find("button[aria-label='New list']").ClickAsync(new MouseEventArgs());
        await cut.Find("input[aria-label='New list name']")
            .InputAsync(new ChangeEventArgs { Value = "Dup" });
        await cut.Find("button[aria-label='Create list and add story']").ClickAsync(new MouseEventArgs());

        cut.Markup.Should().Contain("already have a list named");
        _service.LastAddCall.Should().BeNull(); // add never fires when create fails
    }
}
