namespace TheCanalaveLibrary.Core;

/// <summary>
/// Render-ready data for one vouch in a <c>VouchList</c> (settled WU21). Composes
/// <see cref="UserCardDto"/> (so vouch list rows reuse the <c>UserCard</c> leaf) with the optional
/// rich-text vouch note and the date.
///
/// <c>VouchText</c> is already-sanitized HTML (the write service ran it through
/// <c>IHtmlSanitizationService</c> before persisting — sanitize-once-on-save). Rendered via
/// <c>RichTextView</c> as <c>MarkupString</c> — never re-sanitized on display. May be null when the
/// voucher chose not to include a note.
/// </summary>
public record VouchDisplayDto(
    UserCardDto User,
    string? VouchText,
    DateTime DateVouched
);
