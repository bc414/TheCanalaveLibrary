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
/// Render tests for <see cref="SavedTagSelectionLoadFlyout"/> (WU43, Feature 15). Covers: hidden for
/// anonymous viewers (the wrapper's bare <c>&lt;AuthorizeView&gt;</c> gate — see
/// <c>layer3-logic.md</c> "Deferring DI Behind AuthorizeView"), list rendering + nickname text-filter
/// + sort, Apply raising <see cref="SavedTagSelectionLoadFlyout.OnApply"/> with hydrated detail, and
/// the row ⋯ menu's Delete action (via the nested <c>ConfirmDialog</c>).
///
/// <b>Not tested here:</b> Overwrite/Rename/Make-public row actions (thin wrappers over
/// <c>ISavedTagSelectionWriteService.UpdateAsync</c>, exercised at the Integration tier by
/// <c>SavedTagSelectionServiceTests</c>); live visual rendering (Stage 6 sign-off).
/// Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class SavedTagSelectionLoadFlyoutTests : BunitContext
{
    private readonly FakeSavedTagSelectionReadService _readService = new();
    private readonly FakeSavedTagSelectionWriteService _writeService = new();
    private readonly BunitAuthorizationContext _auth;

    public SavedTagSelectionLoadFlyoutTests()
    {
        Services.AddScoped<ISavedTagSelectionReadService>(_ => _readService);
        Services.AddScoped<ISavedTagSelectionWriteService>(_ => _writeService);
        Services.AddScoped<IUserSettingsService>(_ => new FakeUserSettingsService());
        _auth = this.AddAuthorization(); // anonymous/not-authorized by default
    }

    [Fact]
    public void Anonymous_RendersNothing()
    {
        IRenderedComponent<SavedTagSelectionLoadFlyout> cut = Render<SavedTagSelectionLoadFlyout>();
        cut.Markup.Should().BeEmpty();
    }

    [Fact]
    public async Task ClickingButton_OpensFlyoutAndListsSelections()
    {
        _auth.SetAuthorized("user");
        _readService.MySelections =
        [
            new SavedTagSelectionSummaryDto(1, "Fluff Combo", "cozy note", false, DateTime.UtcNow, 2, 1)
        ];

        IRenderedComponent<SavedTagSelectionLoadFlyout> cut = Render<SavedTagSelectionLoadFlyout>();
        await cut.Find("button").ClickAsync(new MouseEventArgs());

        cut.Markup.Should().Contain("Fluff Combo");
        cut.Markup.Should().Contain("cozy note");
        cut.Markup.Should().Contain("2 included");
        cut.Markup.Should().Contain("1 excluded");
    }

    [Fact]
    public async Task NicknameFilter_NarrowsListToMatches()
    {
        _auth.SetAuthorized("user");
        _readService.MySelections =
        [
            new SavedTagSelectionSummaryDto(1, "Fluffy AUs", null, false, DateTime.UtcNow, 1, 0),
            new SavedTagSelectionSummaryDto(2, "Dark Multichapter", null, false, DateTime.UtcNow, 1, 0),
        ];

        IRenderedComponent<SavedTagSelectionLoadFlyout> cut = Render<SavedTagSelectionLoadFlyout>();
        await cut.Find("button").ClickAsync(new MouseEventArgs());

        IElement filterInput = cut.Find("input[type='search']");
        await filterInput.InputAsync(new ChangeEventArgs { Value = "fluff" });

        cut.Markup.Should().Contain("Fluffy AUs");
        cut.Markup.Should().NotContain("Dark Multichapter");
    }

    [Fact]
    public async Task NicknameFilter_NoMatches_ShowsNoMatchesMessage()
    {
        _auth.SetAuthorized("user");
        _readService.MySelections =
        [
            new SavedTagSelectionSummaryDto(1, "Fluffy AUs", null, false, DateTime.UtcNow, 1, 0),
        ];

        IRenderedComponent<SavedTagSelectionLoadFlyout> cut = Render<SavedTagSelectionLoadFlyout>();
        await cut.Find("button").ClickAsync(new MouseEventArgs());
        await cut.Find("input[type='search']").InputAsync(new ChangeEventArgs { Value = "zzz-no-match" });

        cut.Markup.Should().Contain("No matches.");
    }

    [Fact]
    public async Task NoSelections_ShowsEmptyStateMessage()
    {
        _auth.SetAuthorized("user");
        IRenderedComponent<SavedTagSelectionLoadFlyout> cut = Render<SavedTagSelectionLoadFlyout>();
        await cut.Find("button").ClickAsync(new MouseEventArgs());

        cut.Markup.Should().Contain("No saved tag selections yet.");
    }

    [Fact]
    public async Task Apply_InvokesOnApplyWithHydratedDetail()
    {
        _auth.SetAuthorized("user");
        _readService.MySelections =
        [
            new SavedTagSelectionSummaryDto(1, "Fluff Combo", null, false, DateTime.UtcNow, 1, 0)
        ];
        TagChipDto includedChip = new() { TagId = 10, TagName = "Adventure", TagTypeId = TagTypeEnum.Genre };
        SavedTagSelectionDetailDto detail = new(1, "Fluff Combo", null, false, 1, [includedChip], []);
        _readService.DetailsById[1] = detail;

        SavedTagSelectionDetailDto? received = null;
        IRenderedComponent<SavedTagSelectionLoadFlyout> cut = Render<SavedTagSelectionLoadFlyout>(p => p
            .Add(c => c.OnApply, (SavedTagSelectionDetailDto d) => received = d));

        await cut.Find("button").ClickAsync(new MouseEventArgs());
        IElement applyButton = cut.FindAll("button").First(b => b.TextContent.Trim() == "Apply");
        await applyButton.ClickAsync(new MouseEventArgs());

        received.Should().NotBeNull();
        received!.Id.Should().Be(1);
        received.IncludedTags.Should().ContainSingle(t => t.TagId == 10);

        // Flyout closes after Apply.
        cut.Markup.Should().NotContain("Saved tag selections");
    }

    [Fact]
    public async Task DeleteAction_ConfirmedViaConfirmDialog_CallsDeleteAsync()
    {
        _auth.SetAuthorized("user");
        _readService.MySelections =
        [
            new SavedTagSelectionSummaryDto(1, "Temp", null, false, DateTime.UtcNow, 1, 0)
        ];

        IRenderedComponent<SavedTagSelectionLoadFlyout> cut = Render<SavedTagSelectionLoadFlyout>();
        await cut.Find("button").ClickAsync(new MouseEventArgs()); // open flyout

        // Open the row's ⋯ menu, then click Delete.
        await cut.Find("button[aria-label='More actions']").ClickAsync(new MouseEventArgs());
        IElement deleteButton = cut.FindAll("button").First(b => b.TextContent.Trim() == "Delete");
        await deleteButton.ClickAsync(new MouseEventArgs());

        // ConfirmDialog is now open — click its destructive Confirm button (bg-danger, unambiguous
        // now that the row's ⋯ menu — the other "Delete" text — has closed).
        cut.Markup.Should().Contain("permanently deleted");
        await cut.Find("button.bg-danger").ClickAsync(new MouseEventArgs());

        _writeService.LastDeletedId.Should().Be(1);
    }
}
