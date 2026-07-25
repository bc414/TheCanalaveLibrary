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
public class StoryPropertiesFormTests : BunitContext
{
    public StoryPropertiesFormTests()
    {
        // EditorView (Blazored.TextEditor / Quill.js) makes
        // JS calls on render. Loose mode accepts any JS invocation without erroring so we can test the
        // form fields without needing a real JS runtime.
        JSInterop.Mode = JSRuntimeMode.Loose;
        // StoryPropertiesForm renders TagSelector children, which inject ITagReadService.
        Services.AddSingleton<ITagReadService>(new FakeTagReadServiceForForm());
        // TagChip and TagSelector inject ISpriteReadService for sprite URL resolution.
        Services.AddSingleton<ISpriteReadService>(new OptimisticSpriteReadService("/sprites/themes"));
    }

    private StoryPropertiesViewModel MakeValidViewModel() => new()
    {
        Title = "Valid Story Title",
        ShortDescription = "A valid short description"
    };

    [Fact]
    public void Form_Renders_TitleInput()
    {
        IRenderedComponent<StoryPropertiesForm> cut = Render<StoryPropertiesForm>(
            p => p.Add(f => f.ViewModel, MakeValidViewModel()));

        cut.Find("input[placeholder='Story title']").Should().NotBeNull();
    }

    [Fact]
    public void Form_Renders_ShortDescriptionTextarea()
    {
        IRenderedComponent<StoryPropertiesForm> cut = Render<StoryPropertiesForm>(
            p => p.Add(f => f.ViewModel, MakeValidViewModel()));

        cut.FindAll("textarea").Should().NotBeEmpty();
    }

    [Fact]
    public void Form_Renders_RatingSelect()
    {
        IRenderedComponent<StoryPropertiesForm> cut = Render<StoryPropertiesForm>(
            p => p.Add(f => f.ViewModel, MakeValidViewModel()));

        // Rating and Status are both selects; at least one select should be present.
        cut.FindAll("select").Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void Form_Renders_InputFile()
    {
        IRenderedComponent<StoryPropertiesForm> cut = Render<StoryPropertiesForm>(
            p => p.Add(f => f.ViewModel, MakeValidViewModel()));

        cut.FindAll("input[type='file']").Should().NotBeEmpty();
    }

    [Fact]
    public void Form_Renders_SubmitButton_WithDefaultLabel()
    {
        IRenderedComponent<StoryPropertiesForm> cut = Render<StoryPropertiesForm>(
            p => p.Add(f => f.ViewModel, MakeValidViewModel()));

        cut.Find("button[type='submit']").TextContent.Trim().Should().Be("Save");
    }

    [Fact]
    public void Form_Renders_SubmitButton_WithCustomLabel()
    {
        IRenderedComponent<StoryPropertiesForm> cut = Render<StoryPropertiesForm>(p =>
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

        IRenderedComponent<StoryPropertiesForm> cut = Render<StoryPropertiesForm>(p =>
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

        IRenderedComponent<StoryPropertiesForm> cut = Render<StoryPropertiesForm>(p =>
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

        IRenderedComponent<StoryPropertiesForm> cut = Render<StoryPropertiesForm>(
            p => p.Add(f => f.ViewModel, vm));

        cut.Markup.Should().Contain("Server says no.");
    }

    [Fact]
    public void IsLoading_True_DisablesSubmitButton()
    {
        StoryPropertiesViewModel vm = MakeValidViewModel();
        vm.IsLoading = true;

        IRenderedComponent<StoryPropertiesForm> cut = Render<StoryPropertiesForm>(
            p => p.Add(f => f.ViewModel, vm));

        cut.Find("button[type='submit']").HasAttribute("disabled").Should().BeTrue();
    }

    // ── "Also posted on" per-link verification (Feature 53, WU39) ────────────────

    private static readonly ExternalPlatformDto Ao3Platform = new(1, "Archive of Our Own", "archiveofourown.org");

    private IRenderedComponent<StoryPropertiesForm> RenderWithSavedLink(
        VerificationStatusEnum status, bool requested, IReadOnlySet<short>? verifiedPlatformIds = null)
    {
        StoryPropertiesViewModel vm = MakeValidViewModel();
        vm.ExternalLinks =
        [
            new StoryExternalLinkEditDto
            {
                StoryExternalLinkId = 42,
                ExternalPlatformId = 1,
                Url = "https://archiveofourown.org/works/123",
                VerificationStatus = status,
                VerificationRequested = requested
            }
        ];

        return Render<StoryPropertiesForm>(p =>
        {
            p.Add(f => f.ViewModel, vm);
            p.Add(f => f.ExternalPlatforms, (IReadOnlyList<ExternalPlatformDto>)[Ao3Platform]);
            p.Add(f => f.VerifiedPlatformIds, verifiedPlatformIds ?? new HashSet<short>());
        });
    }

    [Fact]
    public void SavedLink_UnverifiedPlatform_RequestButtonDisabled_WithHint()
    {
        IRenderedComponent<StoryPropertiesForm> cut = RenderWithSavedLink(
            VerificationStatusEnum.Unverified, requested: false, verifiedPlatformIds: new HashSet<short>());

        cut.Find("button[aria-label='Request verification']").HasAttribute("disabled").Should().BeTrue();
        cut.Markup.Should().Contain("verify your Archive of Our Own account");
        cut.Markup.Should().Contain("Not yet requested");
    }

    [Fact]
    public void SavedLink_VerifiedPlatform_RequestButtonEnabled()
    {
        IRenderedComponent<StoryPropertiesForm> cut = RenderWithSavedLink(
            VerificationStatusEnum.Unverified, requested: false, verifiedPlatformIds: new HashSet<short> { 1 });

        cut.Find("button[aria-label='Request verification']").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public async Task SavedLink_ClickRequestVerification_RaisesCallback_WithLinkId()
    {
        int? raisedId = null;
        StoryPropertiesViewModel vm = MakeValidViewModel();
        vm.ExternalLinks =
        [
            new StoryExternalLinkEditDto
            {
                StoryExternalLinkId = 42,
                ExternalPlatformId = 1,
                Url = "https://archiveofourown.org/works/123",
                VerificationStatus = VerificationStatusEnum.Unverified,
                VerificationRequested = false
            }
        ];

        IRenderedComponent<StoryPropertiesForm> cut = Render<StoryPropertiesForm>(p =>
        {
            p.Add(f => f.ViewModel, vm);
            p.Add(f => f.ExternalPlatforms, (IReadOnlyList<ExternalPlatformDto>)[Ao3Platform]);
            p.Add(f => f.VerifiedPlatformIds, (IReadOnlySet<short>)new HashSet<short> { 1 });
            p.Add(f => f.OnRequestLinkVerification, EventCallback.Factory.Create<int>(this, id => raisedId = id));
        });

        await cut.Find("button[aria-label='Request verification']").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        raisedId.Should().Be(42);
    }

    [Fact]
    public void SavedLink_PendingReview_ShowsPendingLabel_NoRequestButtonDisabledState()
    {
        IRenderedComponent<StoryPropertiesForm> cut = RenderWithSavedLink(
            VerificationStatusEnum.Unverified, requested: true, verifiedPlatformIds: new HashSet<short> { 1 });

        cut.Markup.Should().Contain("Pending moderator review");
    }

    [Fact]
    public void SavedLink_Confirmed_ShowsConfirmedLabel_NoRequestButton()
    {
        IRenderedComponent<StoryPropertiesForm> cut = RenderWithSavedLink(
            VerificationStatusEnum.Verified, requested: true, verifiedPlatformIds: new HashSet<short> { 1 });

        cut.Markup.Should().Contain("Confirmed");
        cut.FindAll("button[aria-label='Request verification']").Should().BeEmpty(
            "a confirmed link has nothing left to request");
    }

    [Fact]
    public void UnsavedLink_NoStatusLabelOrRequestButton()
    {
        // StoryExternalLinkId == 0 (unsaved) — verification status is meaningless until saved.
        StoryPropertiesViewModel vm = MakeValidViewModel();
        vm.ExternalLinks = [new StoryExternalLinkEditDto { ExternalPlatformId = 1, Url = "https://archiveofourown.org/works/123" }];

        IRenderedComponent<StoryPropertiesForm> cut = Render<StoryPropertiesForm>(p =>
        {
            p.Add(f => f.ViewModel, vm);
            p.Add(f => f.ExternalPlatforms, (IReadOnlyList<ExternalPlatformDto>)[Ao3Platform]);
        });

        cut.FindAll("button[aria-label='Request verification']").Should().BeEmpty();
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
