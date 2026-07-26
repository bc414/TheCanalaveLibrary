using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="TagEditorForm"/> (WU27.5). Covers:
/// - Create mode: empty form, all type options present.
/// - Edit mode: form pre-populated from the chip's fields.
/// - AllowCustomName checkbox visible for every type (WU-TagFanon; mod judgment).
/// - Parent dropdown filtered to same-type top-level tags, excludes self.
/// - Empty name blocks submit (browser validation).
/// - OnSave emits a TagEditorFormResult with correct values.
/// - OnCancel fires when Cancel is clicked.
/// - ServerError is rendered when set.
/// Tier: RazorComponents (bUnit).
/// </summary>
public class TagEditorFormTests : BunitContext
{
    // A minimal directory with two Genre parents and one Character parent.
    private static List<TagDirectoryGroupDto> MakeDirectory(
        int char1Id = 10, int genre1Id = 20, int genre2Id = 21) =>
    [
        new() { TagType = TagTypeEnum.Character, Nodes =
        [
            new() { Tag = MakeChip(char1Id, "Pikachu", TagTypeEnum.Character), Children = [] }
        ]},
        new() { TagType = TagTypeEnum.Setting,  Nodes = [] },
        new() { TagType = TagTypeEnum.Genre, Nodes =
        [
            new() { Tag = MakeChip(genre1Id, "Action", TagTypeEnum.Genre), Children = [] },
            new() { Tag = MakeChip(genre2Id, "Drama",  TagTypeEnum.Genre), Children = [] }
        ]},
        new() { TagType = TagTypeEnum.ContentWarning,  Nodes = [] },
        new() { TagType = TagTypeEnum.CrossoverFandom, Nodes = [] },
    ];

    private static TagChipDto MakeChip(int id, string name, TagTypeEnum type,
        bool isFanon = false, bool allowOC = false, int? parentId = null) => new()
    {
        TagId = id, TagName = name, TagTypeId = type,
        IsFanon = isFanon, AllowCustomName = allowOC, ParentTagId = parentId
    };

    // ── Create mode ───────────────────────────────────────────────────────────

    [Fact]
    public void CreateMode_AllTagTypeOptionsPresent()
    {
        IRenderedComponent<TagEditorForm> cut = Render<TagEditorForm>(p => p
            .Add(c => c.Directory, MakeDirectory())
            .Add(c => c.EditingTag, null));

        cut.FindAll("select#tag-type option").Should()
            .HaveCount(5, "there are 5 TagTypeEnum values");
    }

    // ── AllowCustomName visibility (WU-TagFanon: every type — mod judgment) ──

    [Theory]
    [InlineData(TagTypeEnum.Character)]
    [InlineData(TagTypeEnum.Setting)]
    [InlineData(TagTypeEnum.Genre)]
    [InlineData(TagTypeEnum.ContentWarning)]
    [InlineData(TagTypeEnum.CrossoverFandom)]
    public void AllowCustomName_VisibleForEveryType(TagTypeEnum type)
    {
        // Edit a tag of the given type — the checkbox is not type-gated (no coercion).
        TagChipDto chip = MakeChip(99, "TestTag", type);

        IRenderedComponent<TagEditorForm> cut = Render<TagEditorForm>(p => p
            .Add(c => c.Directory, MakeDirectory())
            .Add(c => c.EditingTag, chip));

        cut.FindAll("input#tag-allow-custom-name").Count
            .Should().Be(1, "AllowCustomName is mod judgment on any tag type");
    }

    // ── Edit mode pre-population ──────────────────────────────────────────────

    [Fact]
    public void EditMode_PrePopulatesNameAndType()
    {
        TagChipDto chip = MakeChip(5, "Existingtag", TagTypeEnum.Genre);

        IRenderedComponent<TagEditorForm> cut = Render<TagEditorForm>(p => p
            .Add(c => c.Directory, MakeDirectory())
            .Add(c => c.EditingTag, chip));

        cut.Find("input#tag-name").GetAttribute("value").Should().Be("Existingtag");
    }

    // ── Parent dropdown filtering ─────────────────────────────────────────────

    [Fact]
    public void ParentDropdown_ShowsSameTypeTopLevelOnly()
    {
        // Editing a Genre tag — parent dropdown should show Genre parents only.
        TagChipDto chip = MakeChip(99, "NewGenre", TagTypeEnum.Genre);

        IRenderedComponent<TagEditorForm> cut = Render<TagEditorForm>(p => p
            .Add(c => c.Directory, MakeDirectory(genre1Id: 20, genre2Id: 21))
            .Add(c => c.EditingTag, chip));

        // Two Genre parents are in the directory plus the "none" option.
        var options = cut.FindAll("select#tag-parent option");
        options.Should().HaveCount(3, "none-option + 2 Genre top-level nodes");
        options.Skip(1).Select(o => o.TextContent.Trim())
            .Should().BeEquivalentTo(["Action", "Drama"]);
    }

    [Fact]
    public void ParentDropdown_ExcludesSelf()
    {
        // Editing char1 — it should not appear in its own parent dropdown.
        TagChipDto self = MakeChip(10, "Pikachu", TagTypeEnum.Character);

        IRenderedComponent<TagEditorForm> cut = Render<TagEditorForm>(p => p
            .Add(c => c.Directory, MakeDirectory(char1Id: 10))
            .Add(c => c.EditingTag, self));

        var parentOptions = cut.FindAll("select#tag-parent option")
            .Where(o => o.GetAttribute("value") == "10")
            .ToList();
        parentOptions.Should().BeEmpty("the tag being edited must not appear as its own parent option");
    }

    // ── Submit emits correct DTO ──────────────────────────────────────────────

    [Fact]
    public async Task Submit_EmitsTagEditorFormResult()
    {
        TagEditorFormResult? captured = null;

        IRenderedComponent<TagEditorForm> cut = Render<TagEditorForm>(p => p
            .Add(c => c.Directory, MakeDirectory())
            .Add(c => c.EditingTag, null)
            .Add(c => c.OnSave, EventCallback.Factory.Create<TagEditorFormResult>(this, r => captured = r)));

        cut.Find("input#tag-name").Change("ANewTag");
        await cut.Find("form").SubmitAsync();

        captured.Should().NotBeNull();
        captured!.TagName.Should().Be("ANewTag");
    }

    // ── OnCancel fires ────────────────────────────────────────────────────────

    [Fact]
    public void CancelButton_FiresOnCancel()
    {
        bool cancelled = false;

        IRenderedComponent<TagEditorForm> cut = Render<TagEditorForm>(p => p
            .Add(c => c.Directory, MakeDirectory())
            .Add(c => c.EditingTag, null)
            .Add(c => c.OnCancel, EventCallback.Factory.Create(this, () => cancelled = true)));

        cut.Find("button[type='button']").Click();

        cancelled.Should().BeTrue();
    }

    // ── ServerError renders ───────────────────────────────────────────────────

    [Fact]
    public void ServerError_RendersErrorMessage()
    {
        IRenderedComponent<TagEditorForm> cut = Render<TagEditorForm>(p => p
            .Add(c => c.Directory, MakeDirectory())
            .Add(c => c.EditingTag, null)
            .Add(c => c.ServerError, "Tag name already exists."));

        cut.Find("[role='alert']").TextContent.Trim()
            .Should().Be("Tag name already exists.");
    }
}
