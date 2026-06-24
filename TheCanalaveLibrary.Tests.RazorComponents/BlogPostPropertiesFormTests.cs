using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render and interaction tests for <see cref="BlogPostPropertiesForm"/> (WU31).
/// Covers: title input present, Rating select present, HasSpoilers checkbox present,
/// publish toggle present, IsLoading disables submit button, story-picker renders when
/// AuthorStories is non-null, story-picker hidden when AuthorStories is null,
/// OnValidSubmit callback raised on valid submit, server-validation errors render.
/// BlogPostPropertiesForm has no @inject (presentational) — no DI setup needed except
/// EditorView JS interop (Loose mode).
/// Not tested here: EditorView rich-text interaction (JS; no interpreter in bUnit).
/// Tier: RazorComponents (bUnit).
/// </summary>
public class BlogPostPropertiesFormTests : TestContext
{
    public BlogPostPropertiesFormTests()
    {
        // EditorView (Blazored.TextEditor / Quill.js) makes JS calls on render.
        // Loose mode accepts any JS invocation without throwing.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static BlogPostPropertiesViewModel MakeValidViewModel() => new()
    {
        Title   = "My Blog Post Title",
        Content = "<p>Some content</p>"
    };

    [Fact]
    public void Form_Renders_TitleInput()
    {
        IRenderedComponent<BlogPostPropertiesForm> cut = RenderComponent<BlogPostPropertiesForm>(
            p => p.Add(f => f.ViewModel, MakeValidViewModel()));

        cut.Find("input[placeholder='Post title']").Should().NotBeNull();
    }

    [Fact]
    public void Form_Renders_RatingSelect()
    {
        IRenderedComponent<BlogPostPropertiesForm> cut = RenderComponent<BlogPostPropertiesForm>(
            p => p.Add(f => f.ViewModel, MakeValidViewModel()));

        cut.FindAll("select").Should().NotBeEmpty();
    }

    [Fact]
    public void Form_Renders_HasSpoilersCheckbox()
    {
        IRenderedComponent<BlogPostPropertiesForm> cut = RenderComponent<BlogPostPropertiesForm>(
            p => p.Add(f => f.ViewModel, MakeValidViewModel()));

        cut.Find("#has-spoilers").Should().NotBeNull();
    }

    [Fact]
    public void Form_Renders_PublishToggle()
    {
        IRenderedComponent<BlogPostPropertiesForm> cut = RenderComponent<BlogPostPropertiesForm>(
            p => p.Add(f => f.ViewModel, MakeValidViewModel()));

        cut.Find("#is-published").Should().NotBeNull();
    }

    [Fact]
    public void Form_Renders_SubmitButton_WithCustomLabel()
    {
        IRenderedComponent<BlogPostPropertiesForm> cut = RenderComponent<BlogPostPropertiesForm>(
            p => p.Add(f => f.ViewModel, MakeValidViewModel())
                  .Add(f => f.SubmitLabel, "Create Post"));

        cut.Find("button[type='submit']").TextContent.Trim().Should().Be("Create Post");
    }

    [Fact]
    public void Form_IsLoading_True_DisablesSubmitButton()
    {
        BlogPostPropertiesViewModel vm = MakeValidViewModel();
        vm.IsLoading = true;

        IRenderedComponent<BlogPostPropertiesForm> cut = RenderComponent<BlogPostPropertiesForm>(
            p => p.Add(f => f.ViewModel, vm));

        cut.Find("button[type='submit']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void Form_AuthorStoriesNull_HidesStoryPicker()
    {
        IRenderedComponent<BlogPostPropertiesForm> cut = RenderComponent<BlogPostPropertiesForm>(
            p => p.Add(f => f.ViewModel, MakeValidViewModel())
                  .Add(f => f.AuthorStories, null));

        // The story-picker's sentinel option "— none —" should not be present.
        cut.Markup.Should().NotContain("— none —");
    }

    [Fact]
    public void Form_AuthorStoriesNonNull_ShowsStoryPickerSelect()
    {
        IReadOnlyList<(int Id, string Title)> stories = [(1, "My Story"), (2, "Another Story")];

        IRenderedComponent<BlogPostPropertiesForm> cut = RenderComponent<BlogPostPropertiesForm>(
            p => p.Add(f => f.ViewModel, MakeValidViewModel())
                  .Add(f => f.AuthorStories, stories));

        // The story-picker's sentinel option and the story titles should be present.
        cut.Markup.Should().Contain("— none —");
        cut.Markup.Should().Contain("My Story");
        cut.Markup.Should().Contain("Another Story");
    }

    [Fact]
    public void Form_ServerValidationErrors_Render()
    {
        BlogPostPropertiesViewModel vm = MakeValidViewModel();
        vm.ServerValidationErrors.Add("Title is required.");
        vm.ServerValidationErrors.Add("Content must not be empty.");

        IRenderedComponent<BlogPostPropertiesForm> cut = RenderComponent<BlogPostPropertiesForm>(
            p => p.Add(f => f.ViewModel, vm));

        cut.FindAll("li").Should().HaveCount(2);
    }

    [Fact]
    public async Task Form_ValidSubmit_RaisesOnValidSubmitCallback()
    {
        bool callbackFired = false;
        IRenderedComponent<BlogPostPropertiesForm> cut = RenderComponent<BlogPostPropertiesForm>(
            p => p.Add(f => f.ViewModel, MakeValidViewModel())
                  .Add(f => f.OnValidSubmit, EventCallback.Factory.Create(this, () => { callbackFired = true; })));

        await cut.Find("form").SubmitAsync();

        callbackFired.Should().BeTrue();
    }
}
