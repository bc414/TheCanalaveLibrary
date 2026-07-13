using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// In-memory stand-in for <see cref="IChapterReadMarkWriteService"/> (WU45) used by
/// <see cref="ChapterListTests"/> and the Story landing composites' tests. Records each manual
/// mark call so tests can assert what the toggle/mark-all dispatched without a host or database.
/// Set <see cref="ThrowOnWrite"/> to exercise the InlineAlert error path.
/// </summary>
public class FakeChapterReadMarkWriteService : IChapterReadMarkWriteService
{
    public List<(int ChapterId, bool IsRead)> SetChapterReadCalls { get; } = [];
    public List<(int StoryId, bool IsRead)> SetAllCalls { get; } = [];
    public bool ThrowOnWrite { get; set; }

    public Task SetChapterReadAsync(int chapterId, bool isRead)
    {
        if (ThrowOnWrite) throw new InvalidOperationException("Simulated write failure.");
        SetChapterReadCalls.Add((chapterId, isRead));
        return Task.CompletedTask;
    }

    public Task SetAllChaptersReadAsync(int storyId, bool isRead)
    {
        if (ThrowOnWrite) throw new InvalidOperationException("Simulated write failure.");
        SetAllCalls.Add((storyId, isRead));
        return Task.CompletedTask;
    }
}
