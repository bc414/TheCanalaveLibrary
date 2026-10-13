using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="NotificationSettingsPage"/> (F43) — the page's first bUnit coverage,
/// added with its "Private messages" section (owner ruling D18, WU-PrivateMessageEmail 2026-10-13: the
/// page is the one home of every optional email, each section read from its owning domain's service).
/// Pins that the section renders an Email toggle and no Collapsed toggle, reflects and writes
/// Messaging's value (never a notification setting), rolls back and explains a refused write, and
/// leaves the category rows as they were. Semantic output and service calls only, no CSS assertions
/// (testing.md §"What belongs in RazorComponents"). Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class NotificationSettingsPageTests : BunitContext
{
    private const string PmToggleSelector = "input[aria-label='Email me about new private messages']";

    private readonly FakeNotificationWriteService _notifications = new()
    {
        Settings =
        [
            new NotificationSettingDto(NotificationTypeEnum.NewFollowerOnYou, NotificationCategoryEnum.YourProfile,
                "New Follower", "Someone followed you.", EmailEnabled: true, Collapsed: false,
                EmailIsDefault: true, CollapsedIsDefault: true),
        ],
    };

    private readonly FakeMessagingWriteService _messaging = new();

    public NotificationSettingsPageTests()
    {
        Services.AddScoped<INotificationWriteService>(_ => _notifications);
        Services.AddScoped<IMessagingWriteService>(_ => _messaging);
        this.AddAuthorization().SetAuthorized("TestUser");
    }

    private static IElement PrivateMessagesSection(IRenderedComponent<NotificationSettingsPage> cut) =>
        cut.Find("section[aria-labelledby='pm-email-heading']");

    [Fact]
    public void ThePrivateMessagesSection_HasAnEmailToggle_AndNoCollapsedToggle()
    {
        IRenderedComponent<NotificationSettingsPage> cut = Render<NotificationSettingsPage>();

        IElement section = PrivateMessagesSection(cut);
        section.QuerySelector("h2")!.TextContent.Trim().Should().Be("Private messages");
        section.QuerySelectorAll("input[type='checkbox']").Should().ContainSingle(
            "Collapsed is a notification-panel concern with no meaning for messages");
        section.TextContent.Should().Contain("Email").And.NotContain("Collapsed");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheToggle_ReflectsMessagingsOwnValue(bool stored)
    {
        _messaging.NewMessageEmailEnabled = stored;

        IRenderedComponent<NotificationSettingsPage> cut = Render<NotificationSettingsPage>();

        cut.Find(PmToggleSelector).HasAttribute("checked").Should().Be(stored);
        _messaging.GetNewMessageEmailCalls.Should().Be(1);
    }

    [Fact]
    public void Unchecking_WritesThroughMessaging_NotTheNotificationSettings()
    {
        IRenderedComponent<NotificationSettingsPage> cut = Render<NotificationSettingsPage>();

        cut.Find(PmToggleSelector).Change(false);

        _messaging.SetNewMessageEmailCalls.Should().Equal(false);
        _notifications.SettingCalls.Should().BeEmpty();
        cut.Find(PmToggleSelector).HasAttribute("checked").Should().BeFalse();
    }

    [Fact]
    public void ARefusedWrite_RollsTheToggleBack_AndSaysWhyInline()
    {
        _messaging.SetNewMessageEmailException = new SessionExpiredException();
        IRenderedComponent<NotificationSettingsPage> cut = Render<NotificationSettingsPage>();

        cut.Find(PmToggleSelector).Change(false);

        cut.Find(PmToggleSelector).HasAttribute("checked").Should().BeTrue("the write was refused, so the stored value stands");
        IElement section = PrivateMessagesSection(cut);
        section.QuerySelector("[role='alert']").Should().NotBeNull("a refusal is shown beside the toggle, not by the page boundary");
        section.QuerySelectorAll("a").Should().Contain(a => a.TextContent.Trim() == "Sign in");
    }

    [Fact]
    public void TheToggle_IsDisabledWhileAWriteIsInFlight()
    {
        _messaging.SetNewMessageEmailGate = new TaskCompletionSource();
        IRenderedComponent<NotificationSettingsPage> cut = Render<NotificationSettingsPage>();

        cut.Find(PmToggleSelector).Change(false);
        cut.Find(PmToggleSelector).HasAttribute("disabled").Should().BeTrue("one write at a time");

        _messaging.SetNewMessageEmailGate.SetResult();
        cut.WaitForAssertion(() => cut.Find(PmToggleSelector).HasAttribute("disabled").Should().BeFalse());
        _messaging.SetNewMessageEmailCalls.Should().Equal(false);
    }

    [Fact]
    public void TheNotificationCategoryRows_StillRender()
    {
        IRenderedComponent<NotificationSettingsPage> cut = Render<NotificationSettingsPage>();

        cut.Markup.Should().Contain("New Follower");
        cut.FindAll("section").Should().HaveCount(2, "one notification category plus Private messages");
    }
}
