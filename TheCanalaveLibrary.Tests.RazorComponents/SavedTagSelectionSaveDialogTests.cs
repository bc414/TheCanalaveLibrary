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
/// Render tests for <see cref="SavedTagSelectionSaveDialog"/> (WU43, Feature 15). Covers: hidden for
/// anonymous viewers, Save disabled when the on-screen tag set is empty, Create called on submit with
/// the entered fields, and validation errors surfaced via <c>InlineAlert</c>.
/// Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class SavedTagSelectionSaveDialogTests : BunitContext
{
    private readonly FakeSavedTagSelectionWriteService _writeService = new();
    private readonly BunitAuthorizationContext _auth;

    public SavedTagSelectionSaveDialogTests()
    {
        Services.AddScoped<ISavedTagSelectionWriteService>(_ => _writeService);
        _auth = this.AddAuthorization(); // anonymous/not-authorized by default
    }

    [Fact]
    public void Anonymous_RendersNothing()
    {
        IRenderedComponent<SavedTagSelectionSaveDialog> cut = Render<SavedTagSelectionSaveDialog>();
        cut.Markup.Should().BeEmpty();
    }

    [Fact]
    public async Task EmptyTagSet_SaveButtonIsDisabled()
    {
        _auth.SetAuthorized("user");
        IRenderedComponent<SavedTagSelectionSaveDialog> cut = Render<SavedTagSelectionSaveDialog>(p => p
            .Add(c => c.IncludedTagIds, [])
            .Add(c => c.ExcludedTagIds, []));

        await cut.Find("button").ClickAsync(new MouseEventArgs()); // open dialog
        await cut.Find("#stsd-nickname").InputAsync(new ChangeEventArgs { Value = "My Combo" });

        IElement saveButton = cut.FindAll("button").First(b => b.TextContent.Trim() == "Save");
        saveButton.HasAttribute("disabled").Should().BeTrue(
            "Save must be disabled when there are no included or excluded tags to capture");
    }

    [Fact]
    public async Task NonEmptyTagSetButNoNickname_SaveButtonIsDisabled()
    {
        _auth.SetAuthorized("user");
        IRenderedComponent<SavedTagSelectionSaveDialog> cut = Render<SavedTagSelectionSaveDialog>(p => p
            .Add(c => c.IncludedTagIds, [1]));

        await cut.Find("button").ClickAsync(new MouseEventArgs());

        IElement saveButton = cut.FindAll("button").First(b => b.TextContent.Trim() == "Save");
        saveButton.HasAttribute("disabled").Should().BeTrue("a nickname is required to save");
    }

    [Fact]
    public async Task ValidInput_ClickingSave_CallsCreateAsyncWithEnteredFields()
    {
        _auth.SetAuthorized("user");
        IRenderedComponent<SavedTagSelectionSaveDialog> cut = Render<SavedTagSelectionSaveDialog>(p => p
            .Add(c => c.IncludedTagIds, [10, 20])
            .Add(c => c.ExcludedTagIds, [30]));

        await cut.Find("button").ClickAsync(new MouseEventArgs()); // open
        await cut.Find("#stsd-nickname").InputAsync(new ChangeEventArgs { Value = "Fluff Combo" });
        await cut.Find("#stsd-description").InputAsync(new ChangeEventArgs { Value = "cozy note" });
        await cut.Find("input[type='checkbox']").ChangeAsync(new ChangeEventArgs { Value = true });

        IElement saveButton = cut.FindAll("button").First(b => b.TextContent.Trim() == "Save");
        await saveButton.ClickAsync(new MouseEventArgs());

        _writeService.LastCreateInput.Should().NotBeNull();
        _writeService.LastCreateInput!.Nickname.Should().Be("Fluff Combo");
        _writeService.LastCreateInput.Description.Should().Be("cozy note");
        _writeService.LastCreateInput.IsPublic.Should().BeTrue();
        _writeService.LastCreateInput.IncludedTagIds.Should().Equal(10, 20);
        _writeService.LastCreateInput.ExcludedTagIds.Should().Equal(30);

        // Dialog closes after a successful save.
        cut.Markup.Should().NotContain("Save current tag selection");
    }

    [Fact]
    public async Task ValidationException_ShowsErrorsViaInlineAlert()
    {
        _auth.SetAuthorized("user");
        _writeService.ThrowOnCreate = new SavedTagSelectionValidationException(["You already have a saved selection named \"Dup\"."]);

        IRenderedComponent<SavedTagSelectionSaveDialog> cut = Render<SavedTagSelectionSaveDialog>(p => p
            .Add(c => c.IncludedTagIds, [1]));

        await cut.Find("button").ClickAsync(new MouseEventArgs());
        await cut.Find("#stsd-nickname").InputAsync(new ChangeEventArgs { Value = "Dup" });

        IElement saveButton = cut.FindAll("button").First(b => b.TextContent.Trim() == "Save");
        await saveButton.ClickAsync(new MouseEventArgs());

        cut.Markup.Should().Contain("already have a saved selection");
        // Dialog stays open on failure so the user can correct the nickname.
        cut.Markup.Should().Contain("Save current tag selection");
    }
}
