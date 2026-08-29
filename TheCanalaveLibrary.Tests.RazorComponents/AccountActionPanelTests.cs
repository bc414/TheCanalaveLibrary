using Bunit;
using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Regression test for a bug found live during WU-AccountEnforcement's browser verification
/// (2026-07-30). The <c>datetime-local</c> input backing the suspension date produces a
/// <see cref="DateTime"/> with <c>Kind=Unspecified</c>; passed straight through to
/// <c>ApplyAccountActionAsync</c>, Npgsql rejected it (<c>ArgumentException: Cannot write DateTime
/// with Kind=Unspecified to PostgreSQL type 'timestamp with time zone'</c>) — that path had only
/// ever been exercised via direct psql/fixture dates before, never through the real form.
///
/// <para>Retargeted from <c>ModUsersPageTests</c> onto <c>AccountActionPanel</c> at
/// WU-UserModeration (2026-08-01), when <c>/mod/reports</c> gained Suspend and the panel was
/// extracted so both hosts share one implementation of this fix. Testing the panel rather than
/// either host is the point: a second inline copy is what this test now structurally prevents.</para>
///
/// Tier: RazorComponents (bUnit).
/// </summary>
public class AccountActionPanelTests : BunitContext
{
    [Fact]
    public async Task Suspend_SubmitsUtcKindDateTime()
    {
        AccountActionPanel.Submission? submitted = null;

        IRenderedComponent<AccountActionPanel> cut = Render<AccountActionPanel>(p => p
            .Add(c => c.TargetLabel, "SomeUser")
            .Add(c => c.Action, ModeratorActionType.SuspendUser)
            .Add(c => c.OnConfirm, s => submitted = s));

        cut.Find("input[type=datetime-local]").Change("2026-08-20T00:00:00");
        cut.Find("textarea").Change("Regression test for the Kind=Unspecified bug.");
        await FindButton(cut, "Confirm").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        submitted.Should().NotBeNull();
        submitted!.SuspendedUntilUtc.Should().NotBeNull();
        submitted.SuspendedUntilUtc!.Value.Kind.Should().Be(DateTimeKind.Utc,
            "an Unspecified-kind DateTime crashes Npgsql's write to a timestamptz column — " +
            "the panel must tag it before handing it to the host");
        submitted.SuspendedUntilUtc.Value.Should().Be(new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc),
            "the label reads '(UTC)' — the moderator's entered clock value must pass through unshifted, only re-tagged");
    }

    [Fact]
    public async Task Warn_RendersNoSuspensionDate_AndSubmitsNullUntil()
    {
        AccountActionPanel.Submission? submitted = null;

        IRenderedComponent<AccountActionPanel> cut = Render<AccountActionPanel>(p => p
            .Add(c => c.TargetLabel, "SomeUser")
            .Add(c => c.Action, ModeratorActionType.WarnUser)
            .Add(c => c.OnConfirm, s => submitted = s));

        cut.FindAll("input[type=datetime-local]").Should().BeEmpty(
            "only Suspend is time-bounded; Warn and Ban have no end date");

        cut.Find("textarea").Change("Please keep it civil.");
        await FindButton(cut, "Confirm").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        submitted.Should().NotBeNull();
        submitted!.SuspendedUntilUtc.Should().BeNull();
        submitted.Reason.Should().Be("Please keep it civil.");
    }

    [Fact]
    public async Task Confirm_WithoutReason_DoesNotSubmit_AndShowsError()
    {
        bool submitted = false;

        IRenderedComponent<AccountActionPanel> cut = Render<AccountActionPanel>(p => p
            .Add(c => c.TargetLabel, "SomeUser")
            .Add(c => c.Action, ModeratorActionType.BanUser)
            .Add(c => c.OnConfirm, _ => submitted = true));

        await FindButton(cut, "Confirm").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        submitted.Should().BeFalse("an account action always carries a reason — it becomes the audit record's ActionTaken");
        cut.Markup.Should().Contain("A reason is required.");
    }

    [Fact]
    public async Task Suspend_WithoutEndDate_DoesNotSubmit_AndShowsError()
    {
        bool submitted = false;

        IRenderedComponent<AccountActionPanel> cut = Render<AccountActionPanel>(p => p
            .Add(c => c.TargetLabel, "SomeUser")
            .Add(c => c.Action, ModeratorActionType.SuspendUser)
            .Add(c => c.OnConfirm, _ => submitted = true));

        cut.Find("textarea").Change("Reason given, date omitted.");
        await FindButton(cut, "Confirm").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        submitted.Should().BeFalse("a suspension with no end date would be an unbounded ban by accident");
        cut.Markup.Should().Contain("Suspension end date is required.");
    }

    // AngleSharp compound-selector fragility (testing.md) — button text isn't a CSS selector;
    // locate by exact TextContent rather than a brittle :contains(), same as
    // GroupFolderManagementPageTests/ConfirmDialogTests.
    private static AngleSharp.Dom.IElement FindButton(IRenderedComponent<AccountActionPanel> cut, string text) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == text);
}
