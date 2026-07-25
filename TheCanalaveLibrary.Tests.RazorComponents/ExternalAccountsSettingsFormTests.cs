using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render + interaction tests for <see cref="ExternalAccountsSettingsForm"/> (Feature 53, WU39).
/// No @inject (presentational, parameter-driven) — matches the SettingsPage sub-form convention.
/// Covers: the public code display, per-platform status text (none/pending/verified/rejected),
/// and the submit callback firing with the entered (platform, url, handle). Tier: RazorComponents (bUnit).
/// </summary>
public class ExternalAccountsSettingsFormTests : BunitContext
{
    private static readonly VerificationPlatformDto Ao3 =
        new(1, "Archive of Our Own", "Add the code to your profile bio.");

    [Fact]
    public void RendersVerificationCode_WhenPresent()
    {
        IRenderedComponent<ExternalAccountsSettingsForm> cut = Render<ExternalAccountsSettingsForm>(p => p
            .Add(f => f.VerificationCode, "TCL-Verify-ABC234")
            .Add(f => f.Platforms, (IReadOnlyList<VerificationPlatformDto>)[Ao3]));

        cut.Markup.Should().Contain("TCL-Verify-ABC234");
    }

    [Fact]
    public void RendersPlacementInstructions_ForPlatformWithNoAccountYet()
    {
        IRenderedComponent<ExternalAccountsSettingsForm> cut = Render<ExternalAccountsSettingsForm>(p => p
            .Add(f => f.Platforms, (IReadOnlyList<VerificationPlatformDto>)[Ao3])
            .Add(f => f.Accounts, (IReadOnlyList<ExternalAccountDto>)[]));

        cut.Markup.Should().Contain("Add the code to your profile bio.");
        cut.FindAll("input").Should().HaveCount(2, "profile URL + handle inputs");
    }

    [Fact]
    public void VerifiedAccount_ShowsConfirmedText_NoInputFields()
    {
        ExternalAccountDto account = new(1, "Archive of Our Own",
            "https://archiveofourown.org/users/gengarlover", "gengarlover", VerificationStatusEnum.Verified, null);

        IRenderedComponent<ExternalAccountsSettingsForm> cut = Render<ExternalAccountsSettingsForm>(p => p
            .Add(f => f.Platforms, (IReadOnlyList<VerificationPlatformDto>)[Ao3])
            .Add(f => f.Accounts, (IReadOnlyList<ExternalAccountDto>)[account]));

        cut.Markup.Should().Contain("Confirmed");
        cut.Markup.Should().Contain("gengarlover");
        cut.FindAll("input").Should().BeEmpty("a confirmed account has nothing left to edit");
    }

    [Fact]
    public void PendingAccount_ShowsPendingText()
    {
        ExternalAccountDto account = new(1, "Archive of Our Own",
            "https://archiveofourown.org/users/gengarlover", "gengarlover", VerificationStatusEnum.Unverified, null);

        IRenderedComponent<ExternalAccountsSettingsForm> cut = Render<ExternalAccountsSettingsForm>(p => p
            .Add(f => f.Platforms, (IReadOnlyList<VerificationPlatformDto>)[Ao3])
            .Add(f => f.Accounts, (IReadOnlyList<ExternalAccountDto>)[account]));

        cut.Markup.Should().Contain("Pending moderator review");
    }

    [Fact]
    public void RejectedAccount_ShowsReasonText()
    {
        ExternalAccountDto account = new(1, "Archive of Our Own",
            "https://archiveofourown.org/users/gengarlover", "gengarlover",
            VerificationStatusEnum.Rejected, "Code not found on profile.");

        IRenderedComponent<ExternalAccountsSettingsForm> cut = Render<ExternalAccountsSettingsForm>(p => p
            .Add(f => f.Platforms, (IReadOnlyList<VerificationPlatformDto>)[Ao3])
            .Add(f => f.Accounts, (IReadOnlyList<ExternalAccountDto>)[account]));

        cut.Markup.Should().Contain("Code not found on profile.");
    }

    [Fact]
    public async Task Submit_RaisesOnSubmitAccount_WithEnteredValues()
    {
        AddExternalAccountRequest? raised = null;

        IRenderedComponent<ExternalAccountsSettingsForm> cut = Render<ExternalAccountsSettingsForm>(p => p
            .Add(f => f.Platforms, (IReadOnlyList<VerificationPlatformDto>)[Ao3])
            .Add(f => f.Accounts, (IReadOnlyList<ExternalAccountDto>)[])
            .Add(f => f.OnSubmitAccount, EventCallback.Factory.Create<AddExternalAccountRequest>(this, r => raised = r)));

        // aria-label selectors, re-queried per step — each Change re-renders and can invalidate a
        // cached element reference from an earlier FindAll (bUnit's own guidance).
        await cut.Find("input[aria-label='Archive of Our Own profile URL']")
            .ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "https://archiveofourown.org/users/gengarlover" });
        await cut.Find("input[aria-label='Archive of Our Own handle']")
            .ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "gengarlover" });
        await cut.Find("button[aria-label='Request review for Archive of Our Own']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        raised.Should().NotBeNull();
        raised!.ExternalPlatformId.Should().Be(1);
        raised.ProfileUrl.Should().Be("https://archiveofourown.org/users/gengarlover");
        raised.Handle.Should().Be("gengarlover");
    }

    [Fact]
    public void Busy_DisablesRequestReviewButton()
    {
        IRenderedComponent<ExternalAccountsSettingsForm> cut = Render<ExternalAccountsSettingsForm>(p => p
            .Add(f => f.Platforms, (IReadOnlyList<VerificationPlatformDto>)[Ao3])
            .Add(f => f.Accounts, (IReadOnlyList<ExternalAccountDto>)[])
            .Add(f => f.Busy, true));

        cut.Find("button").HasAttribute("disabled").Should().BeTrue();
    }
}
