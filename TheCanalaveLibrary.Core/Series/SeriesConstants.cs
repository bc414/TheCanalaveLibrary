namespace TheCanalaveLibrary.Core;

/// <summary>
/// Series-wide validation constants (WU41). <see cref="MaxNameLength"/> mirrors
/// <see cref="Series.Name"/>'s <c>[MaxLength(256)]</c> attribute. <see cref="MaxDescriptionLength"/>
/// is an application-level DTO cap only — <see cref="Series.Description"/> itself has no
/// <c>[MaxLength]</c> (an unbounded <c>text</c> column, L1 frozen at Stage 5); this constant bounds
/// the create/update DTOs without requiring a migration.
/// </summary>
public static class SeriesConstants
{
    /// <summary>Maximum length of <see cref="Series.Name"/> (mirrors the MaxLength attribute).</summary>
    public const int MaxNameLength = 256;

    /// <summary>Application-level cap on <see cref="Series.Description"/> input (mirrors Group's).</summary>
    public const int MaxDescriptionLength = 2048;
}
