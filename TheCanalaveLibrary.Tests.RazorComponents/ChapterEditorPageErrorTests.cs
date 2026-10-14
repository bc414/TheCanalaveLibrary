using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// <see cref="ChapterEditorPage"/>'s secondary handlers (import-as-version, publish, set primary, add
/// version) clear the form's server error before they try (the WU-ErrorHandling2 follow-up, built by
/// WU-TrackerFixes 2026-10-13; this test added by its review fixes). Before, they appended without
/// clearing, so an earlier failure stayed on screen after a success. Publish stands for the four: each
/// calls <c>ClearServerErrors()</c> the same way. Tier: RazorComponents (bUnit).
/// </summary>
public class ChapterEditorPageErrorTests : BunitContext
{
    private const int StoryId = 7;
    private const int ChapterId = 5;
    private const long ContentId = 100;

    private readonly FakeStoryReadService _reads = new()
    {
        EditDto = new StoryEditDTO
        {
            StoryId = StoryId, Title = "A story", Rating = Rating.E, StoryStatusId = StoryStatusEnum.InProgress,
        },
    };

    private Exception? _publishException;
    private readonly List<(int ChapterId, bool Publish)> _publishCalls = [];

    public ChapterEditorPageErrorTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLogging();
        Services.AddSingleton<IStoryReadService>(_reads);

        IChapterWriteService chapters = ScriptedProxy.Create<IChapterWriteService>(out ScriptedProxy script);
        script
            .On(nameof(IChapterWriteService.GetChapterVersionsAsync), _ => Task.FromResult<IReadOnlyList<ChapterVersionDto>>(
                [new ChapterVersionDto(ContentId, VersionOrder: 1, VersionName: null, Rating: null, WordCount: 3, IsPrimary: true)]))
            .On(nameof(IChapterWriteService.GetChapterForEditAsync), _ => Task.FromResult<ChapterReadingDto?>(
                new ChapterReadingDto(ChapterId, StoryId, ChapterNumber: 1, "Chapter one", "<p>Text.</p>", null, null,
                    WordCount: 3, Rating.E, AuthorId: null, "Author", VersionOrder: 1, VersionName: null, PublishDate: null,
                    PreviousChapterNumber: null, NextChapterNumber: null, StoryRating: Rating.E)))
            .On(nameof(IChapterWriteService.GetChapterTocAsync), _ => Task.FromResult<IReadOnlyList<ChapterTocEntryDto>>(
                [new ChapterTocEntryDto(1, "Chapter one", 3, IsPublished: false, HasAlternateVersions: false)]))
            .On(nameof(IChapterWriteService.SetPublishedAsync), args =>
            {
                _publishCalls.Add(((int)args[0]!, (bool)args[1]!));
                return _publishException is null ? Task.CompletedTask : Task.FromException(_publishException);
            });
        Services.AddSingleton(chapters);

        Services.AddScoped<DraftStore>();
        Services.AddSingleton<IToastService>(new ToastService());
        ComponentFactories.AddStub<ChapterFileImport>(); // the form's file import, with a service of its own
    }

    [Fact]
    public async Task Publish_AfterAFailedAttempt_ClearsTheEarlierFailure()
    {
        IRenderedComponent<ChapterEditorPage> cut = Render<ChapterEditorPage>(p => p
            .Add(c => c.StoryId, StoryId)
            .Add(c => c.ChapterNumber, 1));

        _publishException = new SessionExpiredException();
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Publish").ClickAsync(new MouseEventArgs());

        cut.Markup.Should().Contain("session has expired");
        cut.FindAll("a").Count(a => a.TextContent.Trim() == "Sign in").Should().Be(1);

        _publishException = null;
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Publish").ClickAsync(new MouseEventArgs());

        _publishCalls.Should().Equal((ChapterId, true), (ChapterId, true));
        cut.Markup.Should().Contain("Published");
        cut.Markup.Should().NotContain("session has expired", "a success clears the earlier failure");
        cut.FindAll("a").Count(a => a.TextContent.Trim() == "Sign in").Should().Be(0);
    }
}
