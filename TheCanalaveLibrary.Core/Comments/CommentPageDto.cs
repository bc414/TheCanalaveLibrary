namespace TheCanalaveLibrary.Core;

/// <summary>
/// A page of comments. Roots are paginated; their direct replies are included on the same page.
/// Each <see cref="CommentDto"/> carries <see cref="CommentDto.ParentCommentId"/>, so the page
/// is depth-agnostic — WU20's <c>CommentSection</c> assembles the visual tree.
/// <c>TotalRootCount</c> feeds <c>PaginationControls.TotalCount</c> (root comments only, not replies).
/// </summary>
public record CommentPageDto(
    IReadOnlyList<CommentDto> Comments,
    int TotalRootCount);
