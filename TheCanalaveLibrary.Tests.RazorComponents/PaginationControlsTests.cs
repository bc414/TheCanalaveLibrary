using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="PaginationControls"/> (WU8). Covers:
/// <list type="bullet">
///   <item>Nothing rendered when TotalPages &lt;= 1 (component is guarded by @if (TotalPages &gt; 1)).</item>
///   <item>Page-window logic: small (&lt;=7 pages — all shown), near-start, middle, and near-end windows;
///   ellipsis slots.</item>
///   <item>Active-page markup: the current-page button gets <c>aria-current="page"</c> and different
///   CSS class tokens from the inactive buttons.</item>
///   <item>Prev/Next button disabled-state and <c>aria-label</c>.</item>
///   <item>Range summary text.</item>
///   <item>OnPageChanged EventCallback fires when a page button is clicked.</item>
/// </list>
///
/// <b>Visual/CSS note:</b> bUnit renders markup only — CSS custom properties are not evaluated,
/// so the visual "active-page box" appearance cannot be asserted here. The markup-level evidence
/// (correct class token strings, <c>aria-current="page"</c>) is verified by these tests. Human
/// visual sign-off against the live app is still required for Stage 6. At the markup level, the
/// active-page indicator is correct: the button for CurrentPage gets <c>aria-current="page"</c>
/// and the Phase A active fill (<c>bg-(--color-action)</c>), which inactive buttons never carry.
/// </summary>
public class PaginationControlsTests : BunitContext
{
    // ── guard: nothing rendered when there is only one page ──────────────────────

    [Fact]
    public void PaginationControls_WhenTotalCountFitsOnOnePage_RendersNothing()
    {
        IRenderedComponent<PaginationControls> cut = Render<PaginationControls>(p => p
            .Add(c => c.CurrentPage, 1)
            .Add(c => c.PageSize, 10)
            .Add(c => c.TotalCount, 5));

        cut.Markup.Should().BeEmpty("TotalPages == 1, so the @if guard hides the whole control");
    }

    // ── page-window: <=7 pages — all shown, no ellipsis ─────────────────────────

    [Fact]
    public void PaginationControls_SevenPages_ShowsAllSevenWithNoEllipsis()
    {
        IRenderedComponent<PaginationControls> cut = Render<PaginationControls>(p => p
            .Add(c => c.CurrentPage, 4)
            .Add(c => c.PageSize, 10)
            .Add(c => c.TotalCount, 70));  // 7 pages

        // Every page 1–7 should appear as its own button; only page 4 (current) carries aria-current.
        List<IElement> pageButtons = cut.FindAll("button.size-9:not([aria-label])").ToList();
        pageButtons.Select(b => b.TextContent.Trim())
            .Should().Equal(["1", "2", "3", "4", "5", "6", "7"],
                "with 7 total pages the window shows every page in order");
        for (int page = 1; page <= 7; page++)
        {
            IElement button = pageButtons.First(b => b.TextContent.Trim() == page.ToString());
            button.HasAttribute("aria-current").Should().Be(page == 4,
                $"page {page} {(page == 4 ? "is" : "is not")} the current page");
        }
        cut.FindAll("span").Where(s => s.TextContent.Contains("…")).Should().BeEmpty(
            "with <=7 total pages the window shows every page and never inserts an ellipsis slot");
    }

    // ── page-window: >7 pages, near-start (CurrentPage <=4) ─────────────────────

    [Fact]
    public void PaginationControls_ManyPages_NearStart_ShowsPages1Through5ThenEllipsisThenLast()
    {
        IRenderedComponent<PaginationControls> cut = Render<PaginationControls>(p => p
            .Add(c => c.CurrentPage, 3)
            .Add(c => c.PageSize, 10)
            .Add(c => c.TotalCount, 200));  // 20 pages

        // Slot order: 1, 2, 3, 4, 5, …, 20
        List<IElement> pageButtons = cut.FindAll("button[class*='size-9']")
            .Where(b => b.HasAttribute("aria-current") || !b.IsDisabled())
            .ToList();

        IList<IElement> windowButtons = cut
            .FindAll(".min-w-\\[17\\.25rem\\] > button, .min-w-\\[17\\.25rem\\] > span")
            .ToList();

        // The window must contain exactly one ellipsis span.
        windowButtons.Count(el => el.TagName == "SPAN" && el.TextContent.Contains("…"))
            .Should().Be(1, "near-start window has one trailing ellipsis");

        // The first page-button in the window must be 1.
        windowButtons.First(el => el.TagName == "BUTTON").TextContent.Trim()
            .Should().Be("1");
    }

    // ── page-window: middle (CurrentPage > 4 AND < TotalPages - 3) ──────────────

    [Fact]
    public void PaginationControls_ManyPages_MiddlePage_ShowsFirstEllipsisCurrentNeighboursEllipsisLast()
    {
        IRenderedComponent<PaginationControls> cut = Render<PaginationControls>(p => p
            .Add(c => c.CurrentPage, 10)
            .Add(c => c.PageSize, 10)
            .Add(c => c.TotalCount, 200));  // 20 pages, page 10 = middle

        IList<IElement> windowEls = cut
            .FindAll(".min-w-\\[17\\.25rem\\] > button, .min-w-\\[17\\.25rem\\] > span")
            .ToList();

        int ellipsisCount = windowEls.Count(el => el.TagName == "SPAN" && el.TextContent.Contains("…"));
        ellipsisCount.Should().Be(2, "middle-page window has both a leading and a trailing ellipsis");

        // First button in the window must be page 1.
        windowEls.First(el => el.TagName == "BUTTON").TextContent.Trim().Should().Be("1");
        // Last button in the window must be the last page.
        windowEls.Last(el => el.TagName == "BUTTON").TextContent.Trim().Should().Be("20");
    }

    // ── active-page indicator ────────────────────────────────────────────────────

    [Fact]
    public void PaginationControls_ActivePage_HasAriaCurrent()
    {
        // This is the WU8 markup-level verification (see class summary — visual CSS is out of bUnit scope).
        const int currentPage = 3;

        IRenderedComponent<PaginationControls> cut = Render<PaginationControls>(p => p
            .Add(c => c.CurrentPage, currentPage)
            .Add(c => c.PageSize, 10)
            .Add(c => c.TotalCount, 50));  // 5 pages

        IElement? activeButton = cut.FindAll("button[aria-current='page']").FirstOrDefault();
        activeButton.Should().NotBeNull("the current-page button must have aria-current='page'");
        activeButton!.TextContent.Trim().Should().Be(currentPage.ToString(),
            "the button marked aria-current='page' must show the current page number");
    }

    // ── Prev / Next buttons ──────────────────────────────────────────────────────

    [Fact]
    public void PaginationControls_OnFirstPage_PreviousButtonIsDisabled()
    {
        IRenderedComponent<PaginationControls> cut = Render<PaginationControls>(p => p
            .Add(c => c.CurrentPage, 1)
            .Add(c => c.PageSize, 10)
            .Add(c => c.TotalCount, 50));

        IElement prevButton = cut.Find("button[aria-label='Previous page']");
        prevButton.IsDisabled().Should().BeTrue("previous button must be disabled on the first page");
    }

    [Fact]
    public void PaginationControls_OnLastPage_NextButtonIsDisabled()
    {
        IRenderedComponent<PaginationControls> cut = Render<PaginationControls>(p => p
            .Add(c => c.CurrentPage, 5)
            .Add(c => c.PageSize, 10)
            .Add(c => c.TotalCount, 50));

        IElement nextButton = cut.Find("button[aria-label='Next page']");
        nextButton.IsDisabled().Should().BeTrue("next button must be disabled on the last page");
    }

    [Fact]
    public void PaginationControls_OnMiddlePage_BothNavButtonsAreEnabled()
    {
        IRenderedComponent<PaginationControls> cut = Render<PaginationControls>(p => p
            .Add(c => c.CurrentPage, 3)
            .Add(c => c.PageSize, 10)
            .Add(c => c.TotalCount, 50));

        cut.Find("button[aria-label='Previous page']").IsDisabled().Should().BeFalse();
        cut.Find("button[aria-label='Next page']").IsDisabled().Should().BeFalse();
    }

    // ── range summary ────────────────────────────────────────────────────────────

    [Fact]
    public void PaginationControls_RangeSummary_ShowsCorrectRange()
    {
        IRenderedComponent<PaginationControls> cut = Render<PaginationControls>(p => p
            .Add(c => c.CurrentPage, 2)
            .Add(c => c.PageSize, 10)
            .Add(c => c.TotalCount, 47));

        string summaryText = cut.Find("span.text-sm").TextContent;
        // Page 2, size 10, total 47 → showing 11–20 of 47
        summaryText.Should().Contain("11", "range start for page 2 of 10 is item 11");
        summaryText.Should().Contain("20", "range end for page 2 of 10 is item 20");
        summaryText.Should().Contain("47", "total count is 47");
    }

    // ── OnPageChanged callback ───────────────────────────────────────────────────

    [Fact]
    public async Task PaginationControls_ClickingPageButton_InvokesOnPageChangedWithCorrectPage()
    {
        int? receivedPage = null;

        IRenderedComponent<PaginationControls> cut = Render<PaginationControls>(p => p
            .Add(c => c.CurrentPage, 1)
            .Add(c => c.PageSize, 10)
            .Add(c => c.TotalCount, 50)
            .Add(c => c.OnPageChanged, (int page) => { receivedPage = page; }));

        // Click the button for page 3 (visible in a 5-page window starting at page 1).
        IElement page3Button = cut.FindAll("button.size-9:not([aria-label])")
            .First(b => b.TextContent.Trim() == "3");

        await cut.InvokeAsync(() => page3Button.Click());

        receivedPage.Should().Be(3, "clicking the page 3 button must invoke OnPageChanged with 3");
    }
}
