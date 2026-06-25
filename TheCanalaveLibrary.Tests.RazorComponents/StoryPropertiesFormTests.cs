using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render + interaction tests for <see cref="StoryPropertiesForm"/> (WU24).
/// Covers: required fields present, Rating/Status selects present, InputFile present,
/// validation messages fire, OnValidSubmit callback raised on valid submit.
/// StoryPropertiesForm has no @inject (presentational) — no DI setup needed for the form itself,
/// but child TagSelector instances inject ITagReadService, so we register a no-op fake.
///
/// Not tested here: EditorView rich-text interaction (JS interop; JS runtime not available in bUnit
/// without a JS interpreter), TagSelector typeahead (async search, covered in TagSelectorTests),
/// visual Tailwind layout (human Stage 6 sign-off).
/// Tier: RazorComponents (bUnit).
/// </summary>
public class StoryPropertiesFormTests : TestContext
{
    public StoryPropertiesFormTests()
    {
        // EditorView (Blazored.TextEditor / Quill.js) and TagSelector (Blazored.Typeahead) both make
        // JS calls on render. Loose mode accepts any JS invocation without erroring so we can test the
        // form fields without needing a real JS runtime.
        JSInterop.Mode = JSRuntimeMode.Loose;
        // StoryPropertiesForm renders TagSelector children, which inject ITagReadService.
        Services.AddSingleton<ITagReadService>(new FakeTagReadServiceForForm());
    }

    private StoryPropertiesViewModel MakeValidViewModel() => new()
    {
        Title = "Valid Story Title",
        ShortDescription = "A valid short description"
    };

    [Fact]
    public void Form_Renders_TitleInput()
    {
        IRenderedComponent<StoryPropertiesForm> cut = RenderComponent<StoryPropertiesForm>(
            p => p.Add(f => f.ViewModel, MakeValidViewModel()));

        cut.Find("input[placeholder='Story title']").Should().NotBeNull();
    }

    [Fact]
    public void Form_Renders_ShortDescriptionTextarea()
    {
        IRenderedComponent<StoryPropertiesForm> cut = RenderComponent<StoryPropertiesForm>(
            p => p.Add(f => f.ViewModel, MakeValidViewModel()));

        cut.FindAll("textarea").Should().NotBeEmpty();
    }

    [Fact]
    public void Form_Renders_RatingSelect()
    {
        IRenderedComponent<StoryPropertiesForm> cut = RenderComponent<StoryPropertiesForm>(
            p => p.Add(f => f.ViewModel, MakeValidViewModel()));

        // Rating and Status are both selects; at least one select should be present.
        cut.FindAll("select").Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void Form_Renders_InputFile()
    {
        IRenderedComponent<StoryPropertiesForm> cut = RenderComponent<StoryPropertiesForm>(
            p => p.Add(f => f.ViewModel, MakeValidViewModel()));

        cut.FindAll("input[type='file']").Should().NotBeEmpty();
    }

    [Fact]
    public void Form_Renders_SubmitButton_WithDefaultLabel()
    {
        IRenderedComponent<StoryPropertiesForm> cut = RenderComponent<StoryPropertiesForm>(
            p => p.Add(f => f.ViewModel, MakeValidViewModel()));

        cut.Find("button[type='submit']").TextContent.Trim().Should().Be("Save");
    }

    [Fact]
    public void Form_Renders_SubmitButton_WithCustomLabel()
    {
        IRenderedComponent<StoryPropertiesForm> cut = RenderComponent<StoryPropertiesForm>(p =>
        {
            p.Add(f => f.ViewModel, MakeValidViewModel());
            p.Add(f => f.SubmitLabel, "Create Story");
        });

        cut.Find("button[type='submit']").TextContent.Trim().Should().Be("Create Story");
    }

    [Fact]
    public async Task ValidSubmit_RaisesOnValidSubmit_Callback()
    {
        bool callbackFired = false;
        StoryPropertiesViewModel vm = MakeValidViewModel();

        IRenderedComponent<StoryPropertiesForm> cut = RenderComponent<StoryPropertiesForm>(p =>
        {
            p.Add(f => f.ViewModel, vm);
            p.Add(f => f.OnValidSubmit, EventCallback.Factory.Create(this, () => callbackFired = true));
        });

        await cut.Find("form").SubmitAsync();

        callbackFired.Should().BeTrue();
    }

    [Fact]
    public async Task InvalidSubmit_TitleEmpty_DoesNotRaise_OnValidSubmit()
    {
        bool callbackFired = false;
        StoryPropertiesViewModel vm = new()
        {
            Title = string.Empty,      // fails [Required]
            ShortDescription = "ok"
        };

        IRenderedComponent<StoryPropertiesForm> cut = RenderComponent<StoryPropertiesForm>(p =>
        {
            p.Add(f => f.ViewModel, vm);
            p.Add(f => f.OnValidSubmit, EventCallback.Factory.Create(this, () => callbackFired = true));
        });

        await cut.Find("form").SubmitAsync();

        callbackFired.Should().BeFalse();
    }

    [Fact]
    public void ServerValidationErrors_AreRendered_WhenPresent()
    {
        StoryPropertiesViewModel vm = MakeValidViewModel();
        vm.ServerValidationErrors.Add("Server says no.");

        IRenderedComponent<StoryPropertiesForm> cut = RenderComponent<StoryPropertiesForm>(
            p => p.Add(f => f.ViewModel, vm));

        cut.Markup.Should().Contain("Server says no.");
    }

    [Fact]
    public void IsLoading_True_DisablesSubmitButton()
    {
        StoryPropertiesViewModel vm = MakeValidViewModel();
        vm.IsLoading = true;

        IRenderedComponent<StoryPropertiesForm> cut = RenderComponent<StoryPropertiesForm>(
            p => p.Add(f => f.ViewModel, vm));

        cut.Find("button[type='submit']").HasAttribute("disabled").Should().BeTrue();
    }
}

file sealed class FakeTagReadServiceForForm : ITagReadService
{
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
