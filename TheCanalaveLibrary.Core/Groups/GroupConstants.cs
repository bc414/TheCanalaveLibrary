namespace TheCanalaveLibrary.Core;

/// <summary>
/// Group-wide validation constants (settled WU32). MaxContentRating ceiling enforcement is
/// structural (via the waterfall in <c>ServerGroupWriteService</c>), not a constant here.
/// </summary>
public static class GroupConstants
{
    /// <summary>Maximum length of <see cref="Group.GroupName"/> (mirrors the MaxLength attribute).</summary>
    public const int MaxGroupNameLength = 256;

    /// <summary>Maximum length of <see cref="Group.Description"/> (mirrors the MaxLength attribute).</summary>
    public const int MaxDescriptionLength = 2048;
}
