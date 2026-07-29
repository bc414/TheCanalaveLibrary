using AngleSharp.Dom;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="TagFilter"/>'s WU43 additions — the Saved Tag Selections Load/Save
/// controls and the <c>ApplySavedSelectionAsync</c> replace-and-remount path. Pre-existing
/// include/exclude/cross-dedup behavior is unchanged and untested here (no prior <c>TagFilterTests</c>
/// existed; that surface is covered indirectly via <c>ResultsFilterPanelTests</c>).
///
/// <b>Key thing under test:</b> applying a saved selection must not just update internal state — it
/// must force the underlying <c>TagSelector</c>s to visually remount with the new chips (the `@key`
/// fix documented in <c>layer3-logic.md</c> "Forcing a Child to Re-Seed via @key"), and must re-emit
/// <see cref="TagFilter.OnChanged"/> so a hosting <c>ResultsFilterPanel</c> picks up the new selection.
/// Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class TagFilterTests : BunitContext
{
    private readonly FakeSavedTagSelectionReadService _stsReadService = new();
    private readonly FakeSavedTagSelectionWriteService _stsWriteService = new();
    private readonly BunitAuthorizationContext _auth;

    public TagFilterTests()
    {
        Services.AddScoped<ITagReadService>(_ => new FakeTagReadServiceForTagFilter());
        Services.AddSingleton<ISpriteReadService>(new OptimisticSpriteReadService("/sprites/themes"));
        Services.AddScoped<ISavedTagSelectionReadService>(_ => _stsReadService);
        Services.AddScoped<ISavedTagSelectionWriteService>(_ => _stsWriteService);
        Services.AddScoped<IUserSettingsService>(_ => new FakeUserSettingsService());
        JSInterop.Mode = JSRuntimeMode.Loose;
        _auth = this.AddAuthorization();
    }

    [Fact]
    public void Authorized_ShowsLoadAndSaveButtons()
    {
        _auth.SetAuthorized("user");
        IRenderedComponent<TagFilter> cut = Render<TagFilter>();
        cut.Markup.Should().Contain("Load saved");
        cut.Markup.Should().Contain("Save current…");
    }

    [Fact]
    public void AllowSavedSelectionsFalse_HidesLoadAndSaveButtonsEvenWhenAuthorized()
    {
        _auth.SetAuthorized("user");
        IRenderedComponent<TagFilter> cut = Render<TagFilter>(p => p
            .Add(c => c.AllowSavedSelections, false));

        cut.Markup.Should().NotContain("Load saved");
        cut.Markup.Should().NotContain("Save current");
    }

    [Fact]
    public async Task ApplyingSavedSelection_ReplacesChipsAndReemitsOnChanged()
    {
        _auth.SetAuthorized("user");

        TagChipDto includedChip = new() { TagId = 100, TagName = "Adventure", TagTypeId = TagTypeEnum.Genre };
        TagChipDto excludedChip = new() { TagId = 200, TagName = "Angst", TagTypeId = TagTypeEnum.Genre };
        _stsReadService.MySelections =
        [
            new SavedTagSelectionSummaryDto(1, "Fluff Combo", null, false, DateTime.UtcNow, 1, 1)
        ];
        _stsReadService.DetailsById[1] = new SavedTagSelectionDetailDto(
            1, "Fluff Combo", null, false, 1, [includedChip], [excludedChip]);

        TagFilterSelection? emitted = null;
        IRenderedComponent<TagFilter> cut = Render<TagFilter>(p => p
            .Add(c => c.TagTypes, [TagTypeEnum.Genre])
            .Add(c => c.OnChanged, (TagFilterSelection s) => emitted = s));

        // Open the Load flyout and Apply the one saved selection.
        IElement loadButton = cut.FindAll("button").First(b => b.TextContent.Trim() == "Load saved");
        await loadButton.ClickAsync(new MouseEventArgs());
        IElement applyButton = cut.FindAll("button").First(b => b.TextContent.Trim() == "Apply");
        await applyButton.ClickAsync(new MouseEventArgs());

        // The include/exclude TagSelectors must now display the applied chips.
        cut.Markup.Should().Contain("Adventure");
        cut.Markup.Should().Contain("Angst");

        // TagFilter re-emitted the new aggregate selection to its parent (ResultsFilterPanel).
        emitted.Should().NotBeNull();
        emitted!.IncludedTagIds.Should().Equal(100);
        emitted.ExcludedTagIds.Should().Equal(200);
    }

    // ── Late seed arrival (regression: WU-DiscoveryFilterRestore browser pass) ────────

    [Fact]
    public void LateSeed_AfterFirstRender_DisplaysItsChips()
    {
        // Found in the browser, not by the suite: the device-local filter restore resolves chips
        // only once JS interop is available, i.e. AFTER first render. TagFilter used to seed in
        // OnInitialized only, so the selectors sat visibly empty while the query behind them WAS
        // filtered — the worst kind of mismatch, because the page looks unfiltered and isn't.
        IRenderedComponent<TagFilter> cut = Render<TagFilter>(p => p
            .Add(c => c.TagTypes, [TagTypeEnum.Genre]));

        cut.Markup.Should().NotContain("Adventure");

        cut.Render(p => p
            .Add(c => c.TagTypes, [TagTypeEnum.Genre])
            .Add(c => c.IncludedTags, [new TagChipDto { TagId = 100, TagName = "Adventure", TagTypeId = TagTypeEnum.Genre }]));

        cut.Markup.Should().Contain("Adventure",
            "a seed arriving after first render must still reach the TagSelectors (@key remount)");
    }

    [Fact]
    public async Task LateSeed_DoesNotClobberTheUsersOwnEdit()
    {
        // MA-402: once the user has touched the axis, a later seed must lose.
        TagFilterSelection? emitted = null;
        IRenderedComponent<TagFilter> cut = Render<TagFilter>(p => p
            .Add(c => c.TagTypes, [TagTypeEnum.Genre])
            .Add(c => c.IncludedTags, [new TagChipDto { TagId = 100, TagName = "Adventure", TagTypeId = TagTypeEnum.Genre }])
            .Add(c => c.OnChanged, (TagFilterSelection s) => emitted = s));

        // The user removes the seeded chip (only one chip is on screen, so one remove button).
        await cut.Find("button[aria-label='Remove tag']").ClickAsync(new MouseEventArgs());
        emitted!.IncludedTagIds.Should().BeEmpty();

        // A late re-seed of the same content must not resurrect it.
        cut.Render(p => p
            .Add(c => c.TagTypes, [TagTypeEnum.Genre])
            .Add(c => c.IncludedTags, [new TagChipDto { TagId = 100, TagName = "Adventure", TagTypeId = TagTypeEnum.Genre }]));

        cut.Markup.Should().NotContain("Adventure");
    }

    /// <summary>
    /// A fake ITagReadService whose SearchTagChipsAsync is never expected to be exercised in these
    /// tests (no typeahead interaction) — present only so TagSelector (nested under TagFilter) can
    /// construct without a missing-service DI failure.
    /// </summary>
    private sealed class FakeTagReadServiceForTagFilter : ITagReadService
    {
        public Task<List<TagChipDto>> SearchTagChipsAsync(TagTypeEnum type, string term) =>
            Task.FromResult(new List<TagChipDto>());
        public Task<List<TagDropDownDTO>> GetTagsByTypeAsync(TagTypeEnum type) =>
            Task.FromResult(new List<TagDropDownDTO>());
        public Task<List<TagDropDownDTO>> GetAllCharacterTagsAsync() => Task.FromResult(new List<TagDropDownDTO>());
        public Task<List<TagDropDownDTO>> GetAllSettingTagsAsync() => Task.FromResult(new List<TagDropDownDTO>());
        public Task<List<TagDropDownDTO>> GetAllGenreTagsAsync() => Task.FromResult(new List<TagDropDownDTO>());
        public Task<List<TagDropDownDTO>> GetAllContentWarningTagsAsync() => Task.FromResult(new List<TagDropDownDTO>());
        public Task<List<TagChipDto>> GetTagChipsByIdsAsync(IReadOnlyList<int> tagIds) => Task.FromResult(new List<TagChipDto>());
        public Task<List<TagDirectoryGroupDto>> GetTagDirectoryAsync() => Task.FromResult(new List<TagDirectoryGroupDto>());
    }
}
