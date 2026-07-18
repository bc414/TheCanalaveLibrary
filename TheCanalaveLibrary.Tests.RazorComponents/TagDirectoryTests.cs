using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="TagDirectoryPage"/> (WU27.5; retargeted from the former
/// TagDirectoryDesktop composite 2026-07-18, WU-ResponsiveMerge — the page now owns its markup
/// and loads the directory via <see cref="ITagReadService"/>).
/// Covers:
/// - Sections render per type with correct heading.
/// - Parent chip rendered; child chip nested beneath parent.
/// - Bounded types rendered expanded; unbounded types rendered in &lt;details&gt;.
/// - Mod edit/delete controls visible to Moderator/Admin, hidden to anonymous.
/// Tier: RazorComponents (bUnit).
/// </summary>
public class TagDirectoryTests : BunitContext
{
    private readonly BunitAuthorizationContext _auth;

    public TagDirectoryTests()
    {
        Services.AddSingleton<ISpriteReadService>(new OptimisticSpriteReadService("/sprites/themes"));
        Services.AddScoped<ITagReadService>(_ => new FakeTagWriteService(MakeDirectory()));
        Services.AddScoped<ITagWriteService>(_ => new FakeTagWriteService(MakeDirectory()));
        JSInterop.Mode = JSRuntimeMode.Loose;
        _auth = this.AddAuthorization(); // anonymous/not-authorized by default
    }

    // ── Fixture factory ───────────────────────────────────────────────────────

    private static TagChipDto MakeChip(int id, string name, TagTypeEnum type,
        int? parentId = null) => new()
    {
        TagId = id, TagName = name, TagTypeId = type, ParentTagId = parentId
    };

    /// <summary>
    /// Minimal directory: one Character group with a parent (id=1) and a child (id=2),
    /// one Genre group (id=3) — all other groups empty.
    /// </summary>
    private static List<TagDirectoryGroupDto> MakeDirectory() =>
    [
        new() { TagType = TagTypeEnum.Character, Nodes =
        [
            new()
            {
                Tag      = MakeChip(1, "Bulbasaur",  TagTypeEnum.Character),
                Children = [ MakeChip(2, "Bulbachild", TagTypeEnum.Character, parentId: 1) ]
            }
        ]},
        new() { TagType = TagTypeEnum.Setting,  Nodes = [] },
        new() { TagType = TagTypeEnum.Genre, Nodes =
        [
            new() { Tag = MakeChip(3, "Action", TagTypeEnum.Genre), Children = [] }
        ]},
        new() { TagType = TagTypeEnum.ContentWarning,  Nodes = [] },
        new() { TagType = TagTypeEnum.CrossoverFandom, Nodes = [] },
    ];

    // ── Parent chip + nested child ────────────────────────────────────────────

    [Fact]
    public void Page_RendersBulbasaurChipWithNestedChild()
    {
        IRenderedComponent<TagDirectoryPage> cut = Render<TagDirectoryPage>();

        string markup = cut.Markup;
        markup.Should().Contain("Bulbasaur", "parent chip must render");
        markup.Should().Contain("Bulbachild", "child chip must render nested beneath parent");
    }

    // ── Unbounded vs bounded rendering ────────────────────────────────────────

    [Fact]
    public void Page_UnboundedType_RenderedInDetails()
    {
        // Character is unbounded — its section should be in a <details> element.
        IRenderedComponent<TagDirectoryPage> cut = Render<TagDirectoryPage>();

        // Find a <details> that contains the word "Characters"
        bool hasDetails = cut.FindAll("details").Any(d => d.InnerHtml.Contains("Characters"));
        hasDetails.Should().BeTrue("Character (unbounded) section should be collapsible <details>");
    }

    [Fact]
    public void Page_BoundedType_RenderedAsSection_NotDetails()
    {
        // Genre is bounded — its section should be a plain <section>, not a <details>.
        IRenderedComponent<TagDirectoryPage> cut = Render<TagDirectoryPage>();

        // There should be a <section> containing "Genres" and no <details> containing it.
        bool sectionWithGenres = cut.FindAll("section").Any(s => s.InnerHtml.Contains("Genres"));
        bool detailsWithGenres = cut.FindAll("details").Any(d => d.InnerHtml.Contains("Genres"));

        sectionWithGenres.Should().BeTrue("Genre (bounded) renders in a plain <section>");
        detailsWithGenres.Should().BeFalse("Genre (bounded) must not be in a <details>");
    }

    // ── Mod controls: AuthorizeView gating ────────────────────────────────────

    [Fact]
    public void Page_Anonymous_DoesNotRenderModControls()
    {
        // No auth context → no edit/delete buttons, no "+ New Tag".
        IRenderedComponent<TagDirectoryPage> cut = Render<TagDirectoryPage>();

        cut.FindAll("button[title^='Edit']").Should().BeEmpty("anonymous sees no edit buttons");
        cut.FindAll("button[title^='Delete']").Should().BeEmpty("anonymous sees no delete buttons");
        cut.FindAll("button").Where(b => b.TextContent.Contains("New Tag"))
            .Should().BeEmpty("anonymous sees no New Tag button");
    }

    [Fact]
    public void Page_Moderator_RendersModControls()
    {
        _auth.SetAuthorized("mod-user").SetRoles("Moderator");

        IRenderedComponent<TagDirectoryPage> cut = Render<TagDirectoryPage>();

        // "+ New Tag" button should be present.
        cut.FindAll("button").Any(b => b.TextContent.Contains("New Tag"))
            .Should().BeTrue("Moderator sees the New Tag button");
    }
}

/// <summary>
/// <see cref="ITagWriteService"/> fake (which includes the <see cref="ITagReadService"/> half —
/// integrated read+write interface). Directory payload is configurable — the page under test
/// loads it in <c>OnInitializedAsync</c>; writes are no-ops; all other reads return empty.
/// </summary>
internal sealed class FakeTagWriteService(List<TagDirectoryGroupDto>? directory = null) : ITagWriteService
{
    // ── Write half ────────────────────────────────────────────────────────────
    public Task<TagSaveResult> CreateTagAsync(CreateTagDto dto) => Task.FromResult(new TagSaveResult(0, null));
    public Task<string?> UpdateTagAsync(UpdateTagDto dto) => Task.FromResult<string?>(null);
    public Task DeleteTagAsync(int tagId) => Task.CompletedTask;

    // ── Read half (ITagWriteService : ITagReadService) ────────────────────────
    public Task<List<TagDropDownDTO>> GetTagsByTypeAsync(TagTypeEnum type) => Task.FromResult(new List<TagDropDownDTO>());
    public Task<List<TagDropDownDTO>> GetAllCharacterTagsAsync() => Task.FromResult(new List<TagDropDownDTO>());
    public Task<List<TagDropDownDTO>> GetAllSettingTagsAsync() => Task.FromResult(new List<TagDropDownDTO>());
    public Task<List<TagDropDownDTO>> GetAllGenreTagsAsync() => Task.FromResult(new List<TagDropDownDTO>());
    public Task<List<TagDropDownDTO>> GetAllContentWarningTagsAsync() => Task.FromResult(new List<TagDropDownDTO>());
    public Task<List<TagChipDto>> SearchTagChipsAsync(TagTypeEnum type, string term) => Task.FromResult(new List<TagChipDto>());
    public Task<List<TagChipDto>> GetTagChipsByIdsAsync(IReadOnlyList<int> tagIds) => Task.FromResult(new List<TagChipDto>());
    public Task<List<TagDirectoryGroupDto>> GetTagDirectoryAsync() => Task.FromResult(directory ?? []);
}
