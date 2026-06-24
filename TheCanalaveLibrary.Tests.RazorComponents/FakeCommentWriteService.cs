using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// In-memory stand-in for <see cref="ICommentWriteService"/> (which extends
/// <see cref="ICommentReadService"/>) used by <see cref="CommentEditorTests"/>,
/// <see cref="CommentItemTests"/>, and <see cref="CommentSectionTests"/>. Records each
/// call so tests can assert which methods were invoked without needing a host or database.
/// </summary>
public class FakeCommentWriteService : ICommentWriteService
{
    // ── Read tracking ─────────────────────────────────────────────────────────────

    public List<(int ChapterId, int Page, int PageSize)> GetChapterCommentsCalls { get; } = [];

    private CommentPageDto _getResult = new([], 0);

    public void SetGetResult(CommentPageDto result) => _getResult = result;

    public Task<CommentPageDto> GetChapterCommentsAsync(int chapterId, int page, int pageSize)
    {
        GetChapterCommentsCalls.Add((chapterId, page, pageSize));
        return Task.FromResult(_getResult);
    }

    // ── Write tracking ────────────────────────────────────────────────────────────

    public List<PostChapterCommentDto> PostCalls { get; } = [];
    public List<UpdateCommentDto> EditCalls { get; } = [];
    public List<long> DeleteCalls { get; } = [];
    public List<long> ToggleLikeCalls { get; } = [];

    private CommentLikeResultDto _likeResult = new(0, false);

    public void SetLikeResult(CommentLikeResultDto result) => _likeResult = result;

    public Task<long> PostChapterCommentAsync(PostChapterCommentDto dto)
    {
        PostCalls.Add(dto);
        return Task.FromResult(1L);
    }

    public Task EditCommentAsync(UpdateCommentDto dto)
    {
        EditCalls.Add(dto);
        return Task.CompletedTask;
    }

    public Task DeleteCommentAsync(long commentId)
    {
        DeleteCalls.Add(commentId);
        return Task.CompletedTask;
    }

    public Task<CommentLikeResultDto> ToggleLikeAsync(long commentId)
    {
        ToggleLikeCalls.Add(commentId);
        return Task.FromResult(_likeResult);
    }
}
