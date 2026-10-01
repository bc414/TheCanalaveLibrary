using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// <see cref="StoryLifecyclePanel"/> (WU-StoryLifecycle, owner ruling D1): each status renders its
/// own author actions, and each action raises <c>OnTransition</c> with the right TARGET status. The
/// server's transition table decides the result (Unit: <c>StoryLifecycleTests</c>; Integration:
/// <c>StoryLifecycleTests</c>) — this tier only proves the panel asks for the right move.
/// Tier: RazorComponents (bUnit).
/// </summary>
public class StoryLifecyclePanelTests : BunitContext
{
    private readonly List<StoryStatusEnum> _requested = [];

    private IRenderedComponent<StoryLifecyclePanel> RenderPanel(
        StoryStatusEnum status,
        StoryStatusEnum savedPostApproval = StoryStatusEnum.InProgress,
        string? rejectionReason = null,
        bool isBusy = false) =>
        Render<StoryLifecyclePanel>(p =>
        {
            p.Add(c => c.CurrentStatus, status);
            p.Add(c => c.SavedPostApprovalStatus, savedPostApproval);
            p.Add(c => c.RejectionReason, rejectionReason);
            p.Add(c => c.IsBusy, isBusy);
            p.Add(c => c.OnTransition, EventCallback.Factory.Create<StoryStatusEnum>(this, s => _requested.Add(s)));
        });

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<StoryLifecyclePanel> cut, string text) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == text);

    private static IReadOnlyList<string> ButtonLabels(IRenderedComponent<StoryLifecyclePanel> cut) =>
        cut.FindAll("button").Select(b => b.TextContent.Trim()).ToList();

    [Fact]
    public void Draft_SubmitRequestsPendingApproval()
    {
        IRenderedComponent<StoryLifecyclePanel> cut = RenderPanel(StoryStatusEnum.Draft);

        ButtonLabels(cut).Should().Equal("Submit for publication");
        Button(cut, "Submit for publication").Click();

        _requested.Should().Equal(StoryStatusEnum.PendingApproval);
    }

    [Theory]
    [InlineData(StoryStatusEnum.Draft)]      // "not chosen yet"
    [InlineData(StoryStatusEnum.OnHiatus)]   // defined, but not an entry status
    public void Draft_SubmitDisabled_UntilAValidStatusWhenPublishedIsSaved(StoryStatusEnum saved)
    {
        IRenderedComponent<StoryLifecyclePanel> cut = RenderPanel(StoryStatusEnum.Draft, saved);

        Button(cut, "Submit for publication").HasAttribute("disabled").Should().BeTrue();
        cut.Find("[data-testid=lifecycle-submit-hint]").TextContent.Should().Contain("Status when published");
    }

    [Fact]
    public void Draft_SubmitEnabled_WithASavedEntryStatus_AndNoHint()
    {
        IRenderedComponent<StoryLifecyclePanel> cut = RenderPanel(StoryStatusEnum.Draft, StoryStatusEnum.OpenBeta);

        Button(cut, "Submit for publication").HasAttribute("disabled").Should().BeFalse();
        cut.FindAll("[data-testid=lifecycle-submit-hint]").Should().BeEmpty();
    }

    [Fact]
    public void PendingApproval_WithdrawRequestsDraft()
    {
        IRenderedComponent<StoryLifecyclePanel> cut = RenderPanel(StoryStatusEnum.PendingApproval);

        ButtonLabels(cut).Should().Equal("Withdraw");
        Button(cut, "Withdraw").Click();

        _requested.Should().Equal(StoryStatusEnum.Draft);
    }

    [Fact]
    public void Rejected_ShowsReason_AndReturnToDraftRequestsDraft()
    {
        IRenderedComponent<StoryLifecyclePanel> cut =
            RenderPanel(StoryStatusEnum.Rejected, rejectionReason: "Needs a real summary.");

        cut.Find("[data-testid=lifecycle-rejection-reason]").TextContent.Should().Contain("Needs a real summary.");
        ButtonLabels(cut).Should().Equal("Return to draft to revise");
        Button(cut, "Return to draft to revise").Click();

        _requested.Should().Equal(StoryStatusEnum.Draft);
    }

    [Fact]
    public void Published_UpdateStatusRequestsTheChosenPublishedStatus()
    {
        IRenderedComponent<StoryLifecyclePanel> cut = RenderPanel(StoryStatusEnum.InProgress);

        Button(cut, "Update status").HasAttribute("disabled").Should().BeTrue(
            "nothing to update while the picker still shows the current status");
        cut.Find("#story-lifecycle-status").Change(nameof(StoryStatusEnum.Completed));
        Button(cut, "Update status").Click();

        _requested.Should().Equal(StoryStatusEnum.Completed);
    }

    [Fact]
    public void Published_PickerOffersExactlyThePublishedSet()
    {
        IRenderedComponent<StoryLifecyclePanel> cut = RenderPanel(StoryStatusEnum.OnHiatus);

        cut.FindAll("#story-lifecycle-status option").Select(o => o.GetAttribute("value")).Should().Equal(
            nameof(StoryStatusEnum.InProgress), nameof(StoryStatusEnum.Completed), nameof(StoryStatusEnum.OnHiatus),
            nameof(StoryStatusEnum.Cancelled), nameof(StoryStatusEnum.Rewriting), nameof(StoryStatusEnum.OpenBeta));
    }

    [Fact]
    public void Published_Unpublish_AsksForConfirmation_ThenRequestsDraft()
    {
        IRenderedComponent<StoryLifecyclePanel> cut = RenderPanel(StoryStatusEnum.Completed);

        Button(cut, "Unpublish").Click();

        _requested.Should().BeEmpty("unpublishing is destructive — nothing moves until the author confirms");
        AngleSharp.Dom.IElement dialog = cut.Find("[role=dialog]");
        dialog.TextContent.Should().Contain("moderator's review",
            "the copy warns that getting it back may need review (a revoked author re-enters the queue)");
        AngleSharp.Dom.IElement confirm = dialog.QuerySelectorAll("button").Single(b => b.TextContent.Trim() == "Unpublish");
        confirm.ClassList.Should().Contain("bg-danger", "unpublishing is a destructive confirm (layer4-style.md)");

        confirm.Click();

        _requested.Should().Equal(StoryStatusEnum.Draft);
        cut.FindAll("[role=dialog]").Should().BeEmpty();
    }

    [Fact]
    public void Published_Unpublish_Cancelled_RequestsNothing()
    {
        IRenderedComponent<StoryLifecyclePanel> cut = RenderPanel(StoryStatusEnum.Completed);

        Button(cut, "Unpublish").Click();
        cut.Find("[role=dialog]").QuerySelectorAll("button").Single(b => b.TextContent.Trim() == "Cancel").Click();

        _requested.Should().BeEmpty();
        cut.FindAll("[role=dialog]").Should().BeEmpty();
    }

    [Fact]
    public void IsBusy_DisablesEveryAction()
    {
        IRenderedComponent<StoryLifecyclePanel> cut = RenderPanel(StoryStatusEnum.PendingApproval, isBusy: true);

        Button(cut, "Withdraw").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void ShowsTheCurrentStatusLabel()
    {
        IRenderedComponent<StoryLifecyclePanel> cut = RenderPanel(StoryStatusEnum.OpenBeta);

        cut.Find("[data-testid=lifecycle-status]").TextContent.Should().Be("Open Beta");
    }
}
