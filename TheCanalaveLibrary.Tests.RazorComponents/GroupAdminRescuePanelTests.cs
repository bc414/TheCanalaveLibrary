using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// <see cref="GroupAdminRescuePanel"/> — a moderator's remedy for an admin-less group (owner ruling D13
/// edge (b), WU-UserDeletion 2026-10-13). The panel lists the group's members, needs a member and notes
/// before it submits, calls <see cref="IModerationWriteService.AssignGroupAdminAsync"/> with exactly
/// what was picked, and raises <c>OnAssigned</c> so the page reloads. A refusal ("moved under the
/// panel") keeps its message and reloads the member list (<c>error-handling.md</c>). The service's
/// guards are Integration (<c>ModerationServiceTests</c>). Tier: RazorComponents (bUnit).
/// </summary>
public class GroupAdminRescuePanelTests : BunitContext
{
    private const int GroupId = 10;

    private readonly FakeGroupWriteService _groups = new()
    {
        Members =
        [
            new GroupMemberDto(21, "Ash", null, GroupRole.Member, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            new GroupMemberDto(22, "Dawn", null, GroupRole.Member, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)),
        ],
    };

    private readonly FakeModerationWriteService _moderation = new() { AssignGroupAdminCalls = [] };
    private int _assignedRaised;

    public GroupAdminRescuePanelTests()
    {
        Services.AddScoped<IGroupReadService>(_ => _groups);
        Services.AddScoped<IModerationWriteService>(_ => _moderation);
        Services.AddScoped<IToastService>(_ => new FakeToastService());
    }

    private IRenderedComponent<GroupAdminRescuePanel> RenderPanel() =>
        Render<GroupAdminRescuePanel>(p => p
            .Add(c => c.GroupId, GroupId)
            .Add(c => c.OnAssigned, () => _assignedRaised++));

    private static IElement AssignButton(IRenderedComponent<GroupAdminRescuePanel> cut) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Assign as admin");

    [Fact]
    public void ListsTheMembers_WithLabelledControls()
    {
        IRenderedComponent<GroupAdminRescuePanel> cut = RenderPanel();

        cut.FindAll("option").Select(o => o.TextContent.Trim())
            .Should().Equal("Choose a member…", "Ash", "Dawn");
        string selectId = cut.Find("select").Id!;
        cut.Find($"label[for='{selectId}']").TextContent.Should().Be("Member");
        string notesId = cut.Find("textarea").Id!;
        cut.Find($"label[for='{notesId}']").TextContent.Should().Contain("Notes");
        cut.Find("textarea").GetAttribute("maxlength").Should().Be("1024");
    }

    [Fact]
    public void Submit_IsDisabled_UntilAMemberAndNotesAreGiven()
    {
        IRenderedComponent<GroupAdminRescuePanel> cut = RenderPanel();

        AssignButton(cut).HasAttribute("disabled").Should().BeTrue("nothing is picked yet");

        cut.Find("select").Change("22");
        AssignButton(cut).HasAttribute("disabled").Should().BeTrue("notes are required");

        cut.Find("textarea").Input("   ");
        AssignButton(cut).HasAttribute("disabled").Should().BeTrue("whitespace is not a note");

        cut.Find("textarea").Input("Requested by the members in the group's comments");
        AssignButton(cut).HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public void Submit_AssignsThePickedMember_WithTheNotes_AndRaisesOnAssigned()
    {
        IRenderedComponent<GroupAdminRescuePanel> cut = RenderPanel();

        cut.Find("select").Change("22");
        cut.Find("textarea").Input("Requested by the members");
        AssignButton(cut).Click();

        cut.WaitForState(() => _assignedRaised > 0, TimeSpan.FromSeconds(2));
        _moderation.AssignGroupAdminCalls.Should().ContainSingle()
            .Which.Should().Be((GroupId, 22, "Requested by the members"));
        _assignedRaised.Should().Be(1);
    }

    [Fact]
    public void Refusal_KeepsTheMessage_ReloadsTheMembers_AndDoesNotRaiseOnAssigned()
    {
        // Another moderator assigned an admin meanwhile, or the member left: the group moved under the panel.
        _moderation.AssignGroupAdminException = new ModerationValidationException(
            ["That user isn't a member of this group. Only a member can be made its admin."]);
        IRenderedComponent<GroupAdminRescuePanel> cut = RenderPanel();

        cut.Find("select").Change("21");
        cut.Find("textarea").Input("Requested");
        _groups.Members = [_groups.Members[1]]; // Ash left
        AssignButton(cut).Click();

        cut.WaitForState(() => cut.Markup.Contains("isn't a member"), TimeSpan.FromSeconds(2));
        _assignedRaised.Should().Be(0);
        _groups.GetMembersCalls.Should().Be(2, "the refusal reloads the member list");
        cut.FindAll("option").Select(o => o.TextContent.Trim()).Should().Equal("Choose a member…", "Dawn");
        AssignButton(cut).HasAttribute("disabled").Should().BeTrue("the stale pick was cleared");
    }

    [Fact]
    public void MoreMembersThanOnePage_SaysSo()
    {
        _groups.MembersTotal = 250;
        IRenderedComponent<GroupAdminRescuePanel> cut = RenderPanel();

        cut.Markup.Should().Contain("Showing the first 2 of 250 members");
    }
}
