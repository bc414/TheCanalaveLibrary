namespace TheCanalaveLibrary.Core;

/// <summary>
/// A file could not be imported (unreadable, wrong format, over limits, malformed archive…).
/// The <see cref="Exception.Message"/> is deliberately presentation-safe — written for the
/// importing author, shown via <c>InlineAlert</c> (same discipline as
/// <c>ChapterValidationException</c>; see error-handling.md — this type is the sanctioned
/// exception to the "never show raw ex.Message" rule because its messages ARE the UX copy).
/// </summary>
public class ImportException(string message, Exception? inner = null) : Exception(message, inner);
