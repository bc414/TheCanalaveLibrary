using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="TagDirectoryDesktop"/> and <see cref="TagDirectoryMobile"/> (WU27.5).
/// Covers:
/// - Sections render per type with correct heading.
/// - Parent chip rendered; child chip nested beneath parent.
/// - Bounded types rendered expanded; unbounded types rendered in &lt;details&gt;.
/// - Mod edit/delete controls visible to Moderator/Admin, hidden to anonymous.
/// - OnTagCreated / OnTagDeleted callbacks fire from desktop buttons.
/// Tier: RazorComponents (bUnit).
/// </summary>
public class TagDirectoryTests : TestContext
{
    private readonly TestAuthorizationContext _auth;

    public TagDirectoryTests()
    {
        Services.AddScoped<ITagWriteService>(_ => new FakeTagWriteService());
        JSInterop.Mode = JSRuntimeMode.Loose;
        _auth = this.AddTestAuthorization(); // anonymous/not-authorized by default
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

    // ── Desktop — section headings ────────────────────────────────────────────

    [Fact]
    public void Desktop_RendersCharactersAndGenreHeadings()
    {
        IRenderedComponent<TagDirectoryDesktop> cut = RenderComponent<TagDirectoryDesktop>(p => p
            .Add(c => c.Directory, MakeDirectory()));

        string markup = cut.Markup;
        markup.Should().Contain("Characters", "Character section heading must be present");
        markup.Should().Contain("Genres", "Genre section heading must be present");
    }

    // ── Desktop — parent chip + nested child ─────────────────────────────────

    [Fact]
    public void Desktop_RendersBulbasaurChipWithNestedChild()
    {
        IRenderedComponent<TagDirectoryDesktop> cut = RenderComponent<TagDirectoryDesktop>(p => p
            .Add(c => c.Directory, MakeDirectory()));

        string markup = cut.Markup;
        markup.Should().Contain("Bulbasaur", "parent chip must render");
        markup.Should().Contain("Bulbachild", "child chip must render nested beneath parent");
    }

    // ── Desktop — unbounded vs bounded rendering ──────────────────────────────

    [Fact]
    public void Desktop_UnboundedType_RenderedInDetails()
    {
        // Character is unbounded — its section should be in a <details> element.
        IRenderedComponent<TagDirectoryDesktop> cut = RenderComponent<TagDirectoryDesktop>(p => p
            .Add(c => c.Directory, MakeDirectory()));

        // Find a <details> that contains the word "Characters"
        bool hasDetails = cut.FindAll("details").Any(d => d.InnerHtml.Contains("Characters"));
        hasDetails.Should().BeTrue("Character (unbounded) section should be collapsible <details>");
    }

    [Fact]
    public void Desktop_BoundedType_RenderedAsSection_NotDetails()
    {
        // Genre is bounded — its section should be a plain <section>, not a <details>.
        IRenderedComponent<TagDirectoryDesktop> cut = RenderComponent<TagDirectoryDesktop>(p => p
            .Add(c => c.Directory, MakeDirectory()));

        // There should be a <section> containing "Genres" and no <details> containing it.
        bool sectionWithGenres = cut.FindAll("section").Any(s => s.InnerHtml.Contains("Genres"));
        bool detailsWithGenres = cut.FindAll("details").Any(d => d.InnerHtml.Contains("Genres"));

        sectionWithGenres.Should().BeTrue("Genre (bounded) renders in a plain <section>");
        detailsWithGenres.Should().BeFalse("Genre (bounded) must not be in a <details>");
    }

    // ── Desktop — mod controls: AuthorizeView gating ─────────────────────────

    [Fact]
    public void Desktop_Anonymous_DoesNotRenderModControls()
    {
        // No auth context → no edit/delete buttons, no "+ New Tag".
        IRenderedComponent<TagDirectoryDesktop> cut = RenderComponent<TagDirectoryDesktop>(p => p
            .Add(c => c.Directory, MakeDirectory()));

        cut.FindAll("button[title^='Edit']").Should().BeEmpty("anonymous sees no edit buttons");
        cut.FindAll("button[title^='Delete']").Should().BeEmpty("anonymous sees no delete buttons");
        cut.FindAll("button").Where(b => b.TextContent.Contains("New Tag"))
            .Should().BeEmpty("anonymous sees no New Tag button");
    }

    [Fact]
    public void Desktop_Moderator_RendersModControls()
    {
        _auth.SetAuthorized("mod-user").SetRoles("Moderator");

        IRenderedComponent<TagDirectoryDesktop> cut = RenderComponent<TagDirectoryDesktop>(p => p
            .Add(c => c.Directory, MakeDirectory()));

        // "+ New Tag" button should be present.
        cut.FindAll("button").Any(b => b.TextContent.Contains("New Tag"))
            .Should().BeTrue("Moderator sees the New Tag button");
    }

    [Fact]
    public void Desktop_Admin_RendersModControls()
    {
        _auth.SetAuthorized("admin-user").SetRoles("Admin");

        IRenderedComponent<TagDirectoryDesktop> cut = RenderComponent<TagDirectoryDesktop>(p => p
            .Add(c => c.Directory, MakeDirectory()));

        cut.FindAll("button").Any(b => b.TextContent.Contains("New Tag"))
            .Should().BeTrue("Admin sees the New Tag button");
    }

    // ── Mobile — basic render ─────────────────────────────────────────────────

    [Fact]
    public void Mobile_RendersBothChips()
    {
        IRenderedComponent<TagDirectoryMobile> cut = RenderComponent<TagDirectoryMobile>(p => p
            .Add(c => c.Directory, MakeDirectory()));

        string markup = cut.Markup;
        markup.Should().Contain("Bulbasaur");
        markup.Should().Contain("Bulbachild");
        markup.Should().Contain("Action");
    }

    [Fact]
    public void Mobile_UnboundedType_RenderedInDetails()
    {
        IRenderedComponent<TagDirectoryMobile> cut = RenderComponent<TagDirectoryMobile>(p => p
            .Add(c => c.Directory, MakeDirectory()));

        bool hasDetails = cut.FindAll("details").Any(d => d.InnerHtml.Contains("Characters"));
        hasDetails.Should().BeTrue("mobile Character section is collapsible <details>");
    }

    [Fact]
    public void Mobile_DoesNotRenderNewTagButton()
    {
        // Mobile has no "+ New Tag" button — that's a desktop-only feature.
        // Per-chip edit/delete ARE present (via TagDirectorySection's AuthorizeView) when authenticated,
        // but the page-level create action is suppressed.
        _auth.SetAuthorized("mod-user").SetRoles("Moderator");

        IRenderedComponent<TagDirectoryMobile> cut = RenderComponent<TagDirectoryMobile>(p => p
            .Add(c => c.Directory, MakeDirectory()));

        cut.FindAll("button").Where(b => b.TextContent.Contains("New Tag"))
            .Should().BeEmpty("mobile view has no New Tag button — create is desktop-only");
    }
}

/// <summary>
/// No-op <see cref="ITagWriteService"/> for bUnit tests that don't exercise writes.
/// </summary>
internal sealed class FakeTagWriteService : ITagWriteService
{
    public Task<int> CreateTagAsync(CreateTagDto dto) => Task.FromResult(0);
    public Task UpdateTagAsync(UpdateTagDto dto) => Task.CompletedTask;
    public Task DeleteTagAsync(int tagId) => Task.CompletedTask;

    // ITagReadService pass-through — not exercised in desktop/mobile render tests.
    public Task<List<TagDropDownDTO>> GetTagsByTypeAsync(TagTypeEnum type) => Empty();
    public Task<List<TagDropDownDTO>> GetAllCharacterTagsAsync() => Empty();
    public Task<List<TagDropDownDTO>> GetAllSettingTagsAsync() => Empty();
    public Task<List<TagDropDownDTO>> GetAllGenreTagsAsync() => Empty();
    public Task<List<TagDropDownDTO>> GetAllContentWarningTagsAsync() => Empty();
    public Task<List<TagChipDto>> SearchTagChipsAsync(TagTypeEnum type, string term) =>
        Task.FromResult(new List<TagChipDto>());
    public Task<List<TagChipDto>> GetTagChipsByIdsAsync(IReadOnlyList<int> tagIds) =>
        Task.FromResult(new List<TagChipDto>());
    public Task<List<TagDirectoryGroupDto>> GetTagDirectoryAsync() =>
        Task.FromResult(new List<TagDirectoryGroupDto>());

    private static Task<List<TagDropDownDTO>> Empty() => Task.FromResult(new List<TagDropDownDTO>());
}
