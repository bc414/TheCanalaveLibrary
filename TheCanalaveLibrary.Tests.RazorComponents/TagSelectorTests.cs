using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="TagSelector"/> (WU11). The selector injects
/// <see cref="ITagReadService"/> for typeahead search and uses
/// <c>CanalaveTypeahead</c> internally (in-house, Global Flip wave). Tests here cover:
/// <list type="bullet">
///   <item>Pre-selected tags from <see cref="TagSelector.SelectedTags"/> are rendered as chips on
///   init.</item>
///   <item>Removing a pre-selected tag via its chip's remove button fires
///   <see cref="TagSelector.OnSelectionChanged"/> with the updated list.</item>
///   <item>Label parameter is rendered.</item>
/// </list>
///
/// <b>What is NOT tested here:</b> adding a tag via the typeahead (keyboard
/// input → search → selection) requires JavaScript simulation (focus/blur events + JSInterop)
/// that is not straightforward in bUnit. The add-via-search path is covered by manual interaction
/// testing and by the integration-level <c>TagReadServiceTests</c> (which validates the
/// ILike query). The cap-enforcement (≤5 Character / ≤2 Genre) lives in
/// <c>CreateStoryDTO.CanSave()</c> and is covered by <c>StoryValidationsTests</c> in the Unit
/// tier — it is not enforced inside <c>TagSelector</c> itself.
///
/// JSInterop is configured with <see cref="JSRuntimeMode.Loose"/> so that any residual
/// internal JS focus calls don't cause the test to throw.
/// </summary>
public class TagSelectorTests : BunitContext
{
    private readonly FakeTagReadService _fakeTagService = new();

    public TagSelectorTests()
    {
        // Loose JSInterop kept for uniformity with sibling suites (CanalaveTypeahead itself is JS-free).
        // Loose mode silently ignores unexpected calls rather than throwing.
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddScoped<ITagReadService>(_ => _fakeTagService);
        Services.AddSingleton<ISpriteReadService>(new OptimisticSpriteReadService("/sprites/themes"));
    }

    // ── label renders ────────────────────────────────────────────────────────────

    [Fact]
    public void TagSelector_RendersLabel()
    {
        IRenderedComponent<TagSelector> cut = Render<TagSelector>(p => p
            .Add(c => c.TagType, TagTypeEnum.Genre)
            .Add(c => c.Label, "Genre Tags"));

        cut.Markup.Should().Contain("Genre Tags");
    }

    // ── pre-selected tags render as chips ─────────────────────────────────────────

    [Fact]
    public void TagSelector_WithInitialSelectedTags_RendersChipsForEachTag()
    {
        IReadOnlyList<TagChipDto> initial =
        [
            MakeChip(1, "Adventure", TagTypeEnum.Genre),
            MakeChip(2, "Mystery", TagTypeEnum.Genre)
        ];

        IRenderedComponent<TagSelector> cut = Render<TagSelector>(p => p
            .Add(c => c.TagType, TagTypeEnum.Genre)
            .Add(c => c.SelectedTags, initial));

        cut.Markup.Should().Contain("Adventure");
        cut.Markup.Should().Contain("Mystery");
    }

    [Fact]
    public void TagSelector_WithNoInitialSelectedTags_RendersNoChips()
    {
        IRenderedComponent<TagSelector> cut = Render<TagSelector>(p => p
            .Add(c => c.TagType, TagTypeEnum.Genre)
            .Add(c => c.SelectedTags, []));

        // No TagChip components rendered — the span.rounded-full chips are absent.
        cut.FindAll("span.rounded-full").Should().BeEmpty(
            "no initial tags → no chip spans should appear");
    }

    // ── remove flow ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task TagSelector_RemovingATag_FiresOnSelectionChangedWithUpdatedList()
    {
        IReadOnlyList<TagChipDto>? received = null;

        IReadOnlyList<TagChipDto> initial =
        [
            MakeChip(1, "Adventure", TagTypeEnum.Genre),
            MakeChip(2, "Mystery", TagTypeEnum.Genre)
        ];

        IRenderedComponent<TagSelector> cut = Render<TagSelector>(p => p
            .Add(c => c.TagType, TagTypeEnum.Genre)
            .Add(c => c.SelectedTags, initial)
            .Add(c => c.OnSelectionChanged, (IReadOnlyList<TagChipDto> updated) =>
            {
                received = updated;
            }));

        // Click the remove button on the first chip ("Adventure").
        // TagChip renders a <button aria-label="Remove tag"> inside each chip.
        IElement removeButton = cut.FindAll("button[aria-label='Remove tag']").First();
        await removeButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        received.Should().NotBeNull("OnSelectionChanged must be fired when a chip's remove button is clicked");
        received!.Should().HaveCount(1, "one tag was removed — only one remains");
        received.Should().NotContain(t => t.TagName == "Adventure", "the removed tag must not be in the list");
        received.Should().Contain(t => t.TagName == "Mystery", "the kept tag must still be in the list");
    }

    [Fact]
    public async Task TagSelector_RemovingATag_RemovesChipFromRender()
    {
        IReadOnlyList<TagChipDto> initial =
        [
            MakeChip(1, "Adventure", TagTypeEnum.Genre),
            MakeChip(2, "Mystery", TagTypeEnum.Genre)
        ];

        IRenderedComponent<TagSelector> cut = Render<TagSelector>(p => p
            .Add(c => c.TagType, TagTypeEnum.Genre)
            .Add(c => c.SelectedTags, initial)
            .Add(c => c.OnSelectionChanged, (IReadOnlyList<TagChipDto> _) => { }));

        await cut.FindAll("button[aria-label='Remove tag']").First()
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        cut.Markup.Should().NotContain("Adventure", "the removed chip must disappear from the rendered output");
        cut.Markup.Should().Contain("Mystery", "the kept chip must still be rendered");
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    private static TagChipDto MakeChip(int id, string name, TagTypeEnum type) =>
        new() { TagId = id, TagName = name, TagTypeId = type };

    // A minimal fake ITagReadService — TagSelector only calls SearchTagChipsAsync (typeahead),
    // which we return an empty list for so no dropdown search paths are triggered.
    private sealed class FakeTagReadService : ITagReadService
    {
        public Task<List<TagChipDto>> SearchTagChipsAsync(TagTypeEnum type, string term) =>
            Task.FromResult(new List<TagChipDto>());

        public Task<List<TagDropDownDTO>> GetTagsByTypeAsync(TagTypeEnum type) =>
            Task.FromResult(new List<TagDropDownDTO>());

        public Task<List<TagDropDownDTO>> GetAllCharacterTagsAsync() =>
            Task.FromResult(new List<TagDropDownDTO>());

        public Task<List<TagDropDownDTO>> GetAllSettingTagsAsync() =>
            Task.FromResult(new List<TagDropDownDTO>());

        public Task<List<TagDropDownDTO>> GetAllGenreTagsAsync() =>
            Task.FromResult(new List<TagDropDownDTO>());

        public Task<List<TagDropDownDTO>> GetAllContentWarningTagsAsync() =>
            Task.FromResult(new List<TagDropDownDTO>());
        public Task<List<TagChipDto>> GetTagChipsByIdsAsync(IReadOnlyList<int> tagIds) =>
            Task.FromResult(new List<TagChipDto>());
        public Task<List<TagDirectoryGroupDto>> GetTagDirectoryAsync() =>
            Task.FromResult(new List<TagDirectoryGroupDto>());
    }
}
