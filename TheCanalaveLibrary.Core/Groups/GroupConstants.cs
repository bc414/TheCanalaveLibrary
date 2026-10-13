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

    /// <summary>Maximum length of <see cref="GroupFolder.Name"/>, after trimming. The entity's and
    /// <see cref="CreateFolderDto"/>'s MaxLength attributes read it, and
    /// <see cref="GroupFolderValidations.NameErrors"/> enforces it on create and rename.</summary>
    public const int MaxFolderNameLength = 100;
}
