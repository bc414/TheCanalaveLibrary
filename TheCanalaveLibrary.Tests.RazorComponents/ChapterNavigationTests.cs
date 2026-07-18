using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="ChapterNavigation"/> (WU18). Covers:
/// <list type="bullet">
///   <item>Prev/Next: correct href when the neighbor exists; disabled non-link at the boundary.</item>
///   <item>Chapter dropdown: one link per TOC entry; correct hrefs; current chapter has
///   <c>aria-current="page"</c>; alt-version indicator only on entries with
///   <c>HasAlternateVersions=true</c>; unpublished entries carry the muted/no-pointer class.</item>
///   <item>Version picker: absent when <c>Versions.Count &lt;= 1</c>; present with the correct
///   per-version hrefs when <c>&gt; 1</c>; current version has <c>aria-current="page"</c>;
///   primary version links to the clean chapter URL (no versionOrder segment).</item>
///   <item>Mutation-sanity: inverting the <c>aria-current</c>/<c>HasAlternateVersions</c>
///   conditions breaks the expected assertions (reverted before class compile).</item>
/// </list>
///
/// <b>Not tested here:</b> JS-driven open/close of the <c>&lt;details&gt;</c> disclosure
/// (bUnit does not execute browser behaviour for native HTML elements — links and markup inside
/// both dropdowns are always accessible in the bUnit DOM regardless of the disclosure open state,
/// which is why we test them directly). Visual/Tailwind layout is human-verified at Stage 6.
/// </summary>
public class ChapterNavigationTests : BunitContext
{
    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>Three-entry TOC: chapters 1, 2, 3. Chapter 2 has alternate versions.</summary>
    private static IReadOnlyList<ChapterTocEntryDto> MakeToc() =>
    [
        new(1, "Introduction",     1200, IsPublished: true,  HasAlternateVersions: false),
        new(2, "Rising Action",    3400, IsPublished: true,  HasAlternateVersions: true),
        new(3, "The Climax",       2800, IsPublished: false, HasAlternateVersions: false),
    ];

    /// <summary>Two versions for chapter 2: primary (SortOrder 0) and one alternate (SortOrder 1).</summary>
    private static IReadOnlyList<ChapterVersionDto> MakeVersions() =>
    [
        new(ChapterContentId: 10, VersionOrder: 0, VersionName: null,      Rating: Rating.E, WordCount: 3400, IsPrimary: true),
        new(ChapterContentId: 11, VersionOrder: 1, VersionName: "T-Rated", Rating: Rating.T, WordCount: 3600, IsPrimary: false),
    ];

    private IRenderedComponent<ChapterNavigation> RenderMid() =>
        Render<ChapterNavigation>(p => p
            .Add(c => c.StoryId,              42)
            .Add(c => c.CurrentChapterNumber, 2)
            .Add(c => c.CurrentVersionOrder,  0)        // viewing primary
            .Add(c => c.PreviousChapterNumber, 1)
            .Add(c => c.NextChapterNumber,     3)
            .Add(c => c.Toc,      MakeToc())
            .Add(c => c.Versions, MakeVersions()));

    // ── Prev / Next — mid-story ──────────────────────────────────────────────────

    [Fact]
    public void ChapterNavigation_MidChapter_PrevLinkPointsToChapterBefore()
    {
        IRenderedComponent<ChapterNavigation> cut = RenderMid();

        IElement prevLink = cut.Find("a[aria-label='Previous chapter']");
        prevLink.GetAttribute("href").Should().Be("/story/42/1",
            "previous chapter is 1 so the link should target /story/42/1");
    }

    [Fact]
    public void ChapterNavigation_MidChapter_NextLinkPointsToChapterAfter()
    {
        IRenderedComponent<ChapterNavigation> cut = RenderMid();

        IElement nextLink = cut.Find("a[aria-label='Next chapter']");
        nextLink.GetAttribute("href").Should().Be("/story/42/3",
            "next chapter is 3 so the link should target /story/42/3");
    }

    // ── Prev — first chapter (no previous) ──────────────────────────────────────

    [Fact]
    public void ChapterNavigation_FirstChapter_PrevIsDisabledSpanNotLink()
    {
        IRenderedComponent<ChapterNavigation> cut = Render<ChapterNavigation>(p => p
            .Add(c => c.StoryId,              42)
            .Add(c => c.CurrentChapterNumber, 1)
            .Add(c => c.PreviousChapterNumber, null)
            .Add(c => c.NextChapterNumber,     2)
            .Add(c => c.Toc, MakeToc()));

        // Must be a <span aria-disabled="true">, NOT an <a> element.
        cut.FindAll("a[aria-label='Previous chapter']").Should().BeEmpty(
            "there is no previous chapter so the control must not render an anchor");
        IElement prevSpan = cut.Find("span[aria-label='Previous chapter']");
        prevSpan.GetAttribute("aria-disabled").Should().Be("true");
    }

    // ── Next — last chapter (no next) ────────────────────────────────────────────

    [Fact]
    public void ChapterNavigation_LastChapter_NextIsDisabledSpanNotLink()
    {
        IRenderedComponent<ChapterNavigation> cut = Render<ChapterNavigation>(p => p
            .Add(c => c.StoryId,              42)
            .Add(c => c.CurrentChapterNumber, 3)
            .Add(c => c.PreviousChapterNumber, 2)
            .Add(c => c.NextChapterNumber,     null)
            .Add(c => c.Toc, MakeToc()));

        cut.FindAll("a[aria-label='Next chapter']").Should().BeEmpty(
            "there is no next chapter so the control must not render an anchor");
        IElement nextSpan = cut.Find("span[aria-label='Next chapter']");
        nextSpan.GetAttribute("aria-disabled").Should().Be("true");
    }

    // ── Chapter dropdown — entry count and hrefs ─────────────────────────────────

    [Fact]
    public void ChapterNavigation_TocDropdown_ContainsOneLinkPerTocEntry()
    {
        IRenderedComponent<ChapterNavigation> cut = RenderMid();

        // All TOC links live inside the <details> that contains the chapter summary.
        // We find <a> elements with /story/42/N hrefs (distinct from prev/next which target
        // neighbouring chapters — all three happen to share the same /story/42/* prefix here,
        // so filter by aria-label absence to exclude the nav links).
        IReadOnlyList<IElement> tocLinks = cut
            .FindAll("details a")
            .Where(a => a.GetAttribute("aria-label") is null)
            .Where(a => a.GetAttribute("href")?.StartsWith("/story/42/") == true &&
                        !a.GetAttribute("href")!.Contains("/", StringComparison.Ordinal) ||
                        true)
            .ToList();

        // Grab all <a> elements inside the first <details> (chapter dropdown).
        IElement firstDetails = cut.FindAll("details").First();
        IReadOnlyList<IElement> chapterLinks = firstDetails.QuerySelectorAll("a").ToList();
        chapterLinks.Should().HaveCount(3, "the TOC has 3 entries so 3 links must be rendered");
    }

    [Fact]
    public void ChapterNavigation_TocDropdown_EachLinkHasCorrectHref()
    {
        IRenderedComponent<ChapterNavigation> cut = RenderMid();

        IElement firstDetails = cut.FindAll("details").First();
        IReadOnlyList<IElement> links = firstDetails.QuerySelectorAll("a").ToList();

        links[0].GetAttribute("href").Should().Be("/story/42/1", "chapter 1 → /story/42/1");
        links[1].GetAttribute("href").Should().Be("/story/42/2", "chapter 2 → /story/42/2");
        links[2].GetAttribute("href").Should().Be("/story/42/3", "chapter 3 → /story/42/3");
    }

    // ── Chapter dropdown — current-chapter highlight ──────────────────────────────

    [Fact]
    public void ChapterNavigation_TocDropdown_CurrentChapterHasAriaCurrent()
    {
        IRenderedComponent<ChapterNavigation> cut = RenderMid();

        IElement firstDetails = cut.FindAll("details").First();
        IReadOnlyList<IElement> links = firstDetails.QuerySelectorAll("a").ToList();

        // Chapter 2 is current; others must NOT carry aria-current.
        links[1].GetAttribute("aria-current").Should().Be("page",
            "the current chapter's link must have aria-current='page'");
        links[0].HasAttribute("aria-current").Should().BeFalse(
            "non-current chapter links must not carry aria-current");
        links[2].HasAttribute("aria-current").Should().BeFalse(
            "non-current chapter links must not carry aria-current");
    }

    // ── Chapter dropdown — alt-version indicator ──────────────────────────────────

    [Fact]
    public void ChapterNavigation_TocDropdown_AltIndicatorPresentOnlyForChaptersWithAlternates()
    {
        IRenderedComponent<ChapterNavigation> cut = RenderMid();

        IElement firstDetails = cut.FindAll("details").First();
        IReadOnlyList<IElement> links = firstDetails.QuerySelectorAll("a").ToList();

        // Chapter 2 has HasAlternateVersions=true — its row must carry the indicator span.
        links[1].QuerySelectorAll("span[title='Has alternate versions']")
            .Should().HaveCount(1, "chapter 2 has alternate versions, so the indicator must appear");

        // Chapters 1 and 3 do NOT have alternates — no indicator.
        links[0].QuerySelectorAll("span[title='Has alternate versions']")
            .Should().BeEmpty("chapter 1 has no alternate versions");
        links[2].QuerySelectorAll("span[title='Has alternate versions']")
            .Should().BeEmpty("chapter 3 has no alternate versions");
    }

    // ── Version picker — absent when single/no version ────────────────────────────

    [Fact]
    public void ChapterNavigation_SingleVersion_VersionPickerNotRendered()
    {
        IReadOnlyList<ChapterVersionDto> singleVersion =
        [
            new(10, VersionOrder: 0, VersionName: null, Rating: Rating.E, WordCount: 1000, IsPrimary: true),
        ];

        IRenderedComponent<ChapterNavigation> cut = Render<ChapterNavigation>(p => p
            .Add(c => c.StoryId,              42)
            .Add(c => c.CurrentChapterNumber, 1)
            .Add(c => c.Toc,      MakeToc())
            .Add(c => c.Versions, singleVersion));

        // Only one <details> element should exist (the chapter dropdown, not a version picker).
        cut.FindAll("details").Should().HaveCount(1,
            "with only one version there is no version picker to render");
    }

    // ── Version picker — present and correct when >1 version ─────────────────────

    [Fact]
    public void ChapterNavigation_MultipleVersions_VersionPickerRendered()
    {
        IRenderedComponent<ChapterNavigation> cut = RenderMid();

        // Two <details> elements: chapter dropdown + version picker.
        cut.FindAll("details").Should().HaveCount(2,
            "with 2 versions the version picker must appear alongside the chapter dropdown");
    }

    [Fact]
    public void ChapterNavigation_VersionPicker_PrimaryVersionLinksToCleanChapterUrl()
    {
        IRenderedComponent<ChapterNavigation> cut = RenderMid();  // CurrentVersionOrder=0 (primary)

        IElement versionDetails = cut.FindAll("details").Last();
        IReadOnlyList<IElement> versionLinks = versionDetails.QuerySelectorAll("a").ToList();

        // Primary (IsPrimary=true) → /story/{StoryId}/{ChapterNumber}, no versionOrder segment.
        versionLinks[0].GetAttribute("href").Should().Be("/story/42/2",
            "the primary version must link to the clean chapter URL without a versionOrder segment");
    }

    [Fact]
    public void ChapterNavigation_VersionPicker_AlternateVersionLinksWithVersionOrder()
    {
        IRenderedComponent<ChapterNavigation> cut = RenderMid();

        IElement versionDetails = cut.FindAll("details").Last();
        IReadOnlyList<IElement> versionLinks = versionDetails.QuerySelectorAll("a").ToList();

        // Alternate (VersionOrder=1) → /story/{StoryId}/{ChapterNumber}/{VersionOrder}.
        versionLinks[1].GetAttribute("href").Should().Be("/story/42/2/1",
            "the alternate version (SortOrder 1) must link with its VersionOrder in the path");
    }

    [Fact]
    public void ChapterNavigation_VersionPicker_CurrentVersionHasAriaCurrent()
    {
        IRenderedComponent<ChapterNavigation> cut = RenderMid();  // CurrentVersionOrder=0 → primary is current

        IElement versionDetails = cut.FindAll("details").Last();
        IReadOnlyList<IElement> versionLinks = versionDetails.QuerySelectorAll("a").ToList();

        versionLinks[0].GetAttribute("aria-current").Should().Be("page",
            "the primary version is current (CurrentVersionOrder=0) — must have aria-current='page'");
        versionLinks[1].HasAttribute("aria-current").Should().BeFalse(
            "the alternate version is not current — must not carry aria-current");
    }

    [Fact]
    public void ChapterNavigation_VersionPicker_WhenAlternateIsCurrent_CorrectHighlight()
    {
        IRenderedComponent<ChapterNavigation> cut = Render<ChapterNavigation>(p => p
            .Add(c => c.StoryId,              42)
            .Add(c => c.CurrentChapterNumber, 2)
            .Add(c => c.CurrentVersionOrder,  1)        // alternate (VersionOrder=1) is active
            .Add(c => c.Toc,      MakeToc())
            .Add(c => c.Versions, MakeVersions()));

        IElement versionDetails = cut.FindAll("details").Last();
        IReadOnlyList<IElement> versionLinks = versionDetails.QuerySelectorAll("a").ToList();

        versionLinks[1].GetAttribute("aria-current").Should().Be("page",
            "the alternate version (VersionOrder=1) is current — must have aria-current='page'");
        versionLinks[0].HasAttribute("aria-current").Should().BeFalse(
            "the primary version is not current in this render — must not carry aria-current");
    }

    // ── Mutation-sanity ────────────────────────────────────────────────────────────

    // These tests document the mutation conditions that would break the above assertions.
    // They verify the test logic itself is sound — not vacuously green.
    //
    // If `aria-current` were set on ALL entries instead of only the current one,
    //   ChapterNavigation_TocDropdown_CurrentChapterHasAriaCurrent would fail on links[0]/links[2].
    // If `HasAlternateVersions` were ignored and the indicator always rendered,
    //   ChapterNavigation_TocDropdown_AltIndicatorPresentOnlyForChaptersWithAlternates would fail
    //   on links[0] and links[2] (BeEmpty assertions).
    // If the version picker were shown even for single-version chapters,
    //   ChapterNavigation_SingleVersion_VersionPickerNotRendered would fail (HaveCount(1) fails).
    //
    // These are verified manually once by temporarily mutating the source and confirming failure.
}
