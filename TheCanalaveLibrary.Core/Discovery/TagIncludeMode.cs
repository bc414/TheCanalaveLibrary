namespace TheCanalaveLibrary.Core;

/// <summary>
/// Controls how multiple included tag IDs are combined in a story filter (WU28, spec §5.3).
/// Applies to the <c>IncludedTagIds</c> axis of <see cref="StoryFilterDto"/> only; the exclude
/// axis always uses ANY/none semantics and has no mode flag.
/// </summary>
public enum TagIncludeMode : byte
{
    /// <summary>
    /// Story must have <em>all</em> of the included tag IDs (AND across tags).
    /// Default — matches the existing conjunctive loop behaviour. Bookshelves and Profile pages
    /// always use this mode.
    /// </summary>
    And = 0,

    /// <summary>
    /// Story must have <em>at least one</em> of the included tag IDs (OR across tags).
    /// Available on <c>/discover</c> only via the <c>TagFilter.AllowIncludeModeToggle</c> param.
    /// Translates to a single <c>WHERE EXISTS (... IN (...))</c> in SQL rather than a per-tag loop.
    /// </summary>
    Or = 1,
}
