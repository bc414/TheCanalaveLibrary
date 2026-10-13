using System.Reflection;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// <see cref="ChapterEditorPage"/> reads its story through <see cref="IStoryReadService.GetStoryForEditAsync"/>
/// for the rating. Since the WU-AuthorStoryDelete review fixes (2026-10-13) that read reaches the author of
/// a taken-down story (owner ruling D15: the story editor's delete stays reachable), flagged
/// <see cref="StoryEditDTO.IsTakenDown"/>. The chapter editor must stay closed for such a story, as it was
/// when the read returned null: a NotFound, not a new-chapter form. The live-story case proves the same
/// setup does render the form, so the taken-down case fails on its assertion if the guard is removed.
/// Tier: RazorComponents (bUnit).
/// </summary>
public class ChapterEditorPageTakedownTests : BunitContext
{
    private const int StoryId = 7;

    private readonly FakeStoryReadService _reads = new();

    public ChapterEditorPageTakedownTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLogging();
        Services.AddSingleton<IStoryReadService>(_reads);
        Services.AddSingleton(DispatchProxy.Create<IChapterWriteService, UnexpectedCall>());
        Services.AddScoped<DraftStore>();
        Services.AddSingleton<IToastService>(new ToastService());
        ComponentFactories.AddStub<ChapterFileImport>(); // the form's file import, with a service of its own
    }

    private IRenderedComponent<ChapterEditorPage> RenderNewChapter(bool storyTakenDown, out bool notFound)
    {
        _reads.EditDto = new StoryEditDTO
        {
            StoryId = StoryId, Title = "A story", Rating = Rating.E,
            StoryStatusId = StoryStatusEnum.InProgress, IsTakenDown = storyTakenDown,
        };
        bool raised = false;
        Services.GetRequiredService<NavigationManager>().OnNotFound += (_, _) => raised = true;
        IRenderedComponent<ChapterEditorPage> cut = Render<ChapterEditorPage>(p => p.Add(c => c.StoryId, StoryId));
        notFound = raised;
        return cut;
    }

    [Fact]
    public void ATakenDownStory_HasNoChapterEditor()
    {
        IRenderedComponent<ChapterEditorPage> cut = RenderNewChapter(storyTakenDown: true, out bool notFound);

        notFound.Should().BeTrue("a taken-down story's chapters stay closed to editing, as before D15's fix");
        cut.FindAll("form").Should().BeEmpty();
        cut.Markup.Should().NotContain("New Chapter");
    }

    [Fact]
    public void ALiveStory_OpensTheNewChapterForm()
    {
        IRenderedComponent<ChapterEditorPage> cut = RenderNewChapter(storyTakenDown: false, out bool notFound);

        notFound.Should().BeFalse();
        cut.Markup.Should().Contain("New Chapter");
        cut.FindAll("form").Should().NotBeEmpty();
    }

    /// <summary>A chapter-write service no test here should reach: the new-chapter route makes no chapter
    /// call before the form is submitted.</summary>
    public class UnexpectedCall : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new NotSupportedException($"{targetMethod?.Name} is not expected in this test.");
    }
}
