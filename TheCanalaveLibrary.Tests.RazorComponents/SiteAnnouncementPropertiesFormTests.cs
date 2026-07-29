using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render and interaction tests for <see cref="SiteAnnouncementPropertiesForm"/> (WU-SiteNews) —
/// the <c>SiteBlogPost</c> counterpart to <see cref="BlogPostPropertiesForm"/>, minus
/// Rating/HasSpoilers/story-picker, plus the NotifyAllUsers checkbox. Covers: title input
/// present, NotifyAllUsers checkbox present, publish toggle present, IsLoading disables submit
/// button, OnValidSubmit callback raised on valid submit, server-validation errors render. No
/// @inject (presentational) — no DI setup needed except EditorView JS interop (Loose mode).
/// Not tested here: EditorView rich-text interaction (JS; no interpreter in bUnit).
/// Tier: RazorComponents (bUnit).
/// </summary>
public class SiteAnnouncementPropertiesFormTests : BunitContext
{
    public SiteAnnouncementPropertiesFormTests()
    {
        // EditorView (Blazored.TextEditor / Quill.js) makes JS calls on render.
        // Loose mode accepts any JS invocation without throwing.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static SiteAnnouncementPropertiesViewModel MakeValidViewModel() => new()
    {
        Title   = "Beta invitations are open",
        Content = "<p>Some content</p>"
    };

    [Fact]
    public void Form_Renders_SubmitButton_WithCustomLabel()
    {
        IRenderedComponent<SiteAnnouncementPropertiesForm> cut = Render<SiteAnnouncementPropertiesForm>(
            p => p.Add(f => f.ViewModel, MakeValidViewModel())
                  .Add(f => f.SubmitLabel, "Create Announcement"));

        cut.Find("button[type='submit']").TextContent.Trim().Should().Be("Create Announcement");
    }

    [Fact]
    public void Form_IsLoading_True_DisablesSubmitButton()
    {
        SiteAnnouncementPropertiesViewModel vm = MakeValidViewModel();
        vm.IsLoading = true;

        IRenderedComponent<SiteAnnouncementPropertiesForm> cut = Render<SiteAnnouncementPropertiesForm>(
            p => p.Add(f => f.ViewModel, vm));

        cut.Find("button[type='submit']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void Form_Renders_NotifyAllUsersCheckbox()
    {
        IRenderedComponent<SiteAnnouncementPropertiesForm> cut = Render<SiteAnnouncementPropertiesForm>(
            p => p.Add(f => f.ViewModel, MakeValidViewModel()));

        cut.Find("#notify-all-users").Should().NotBeNull();
        cut.Markup.Should().Contain("Notify every user");
    }

    [Fact]
    public void Form_Renders_PublishToggle()
    {
        IRenderedComponent<SiteAnnouncementPropertiesForm> cut = Render<SiteAnnouncementPropertiesForm>(
            p => p.Add(f => f.ViewModel, MakeValidViewModel()));

        cut.Find("#is-published").Should().NotBeNull();
    }

    [Fact]
    public void Form_DoesNotRender_RatingOrSpoilerOrStoryPicker()
    {
        // Structural difference from BlogPostPropertiesForm — these fields aren't meaningful for
        // staff announcements and must not appear.
        IRenderedComponent<SiteAnnouncementPropertiesForm> cut = Render<SiteAnnouncementPropertiesForm>(
            p => p.Add(f => f.ViewModel, MakeValidViewModel()));

        cut.Markup.Should().NotContain("Linked Story");
        cut.Markup.Should().NotContain("Contains spoilers");
    }

    [Fact]
    public void Form_ServerValidationErrors_Render()
    {
        SiteAnnouncementPropertiesViewModel vm = MakeValidViewModel();
        vm.ServerValidationErrors.Add("Title is required.");
        vm.ServerValidationErrors.Add("Content must not be empty.");

        IRenderedComponent<SiteAnnouncementPropertiesForm> cut = Render<SiteAnnouncementPropertiesForm>(
            p => p.Add(f => f.ViewModel, vm));

        cut.FindAll("li").Should().HaveCount(2);
    }

    [Fact]
    public async Task Form_ValidSubmit_RaisesOnValidSubmitCallback()
    {
        bool callbackFired = false;
        IRenderedComponent<SiteAnnouncementPropertiesForm> cut = Render<SiteAnnouncementPropertiesForm>(
            p => p.Add(f => f.ViewModel, MakeValidViewModel())
                  .Add(f => f.OnValidSubmit, EventCallback.Factory.Create(this, () => { callbackFired = true; })));

        await cut.Find("form").SubmitAsync();

        callbackFired.Should().BeTrue();
    }
}
