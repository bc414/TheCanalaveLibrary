namespace TheCanalaveLibrary.Core;

/// <summary>
/// Tier-2 rules and refusal texts for group folders (schema audit §2.4, service audit §2.10;
/// WU-SchemaHardening 2026-10-13). <c>ServerGroupWriteService</c> calls <see cref="NameErrors"/> on
/// create and rename and throws <see cref="GroupValidationException"/> with the result. Rule text:
/// <c>layer2-services.md</c> §"Group Folder Integrity".
/// </summary>
public static class GroupFolderValidations
{
    /// <summary>Refusal for a null, empty or whitespace-only name.</summary>
    public const string NameRequired = "A folder name is required.";

    /// <summary>Refusal for a name longer than <see cref="GroupConstants.MaxFolderNameLength"/> once trimmed.</summary>
    public static readonly string NameTooLong =
        $"Folder names must be {GroupConstants.MaxFolderNameLength} characters or fewer.";

    /// <summary>
    /// The errors for a proposed folder name; empty means valid. The name is measured after trimming,
    /// which is the form the service stores.
    /// </summary>
    public static List<string> NameErrors(string? name)
    {
        string trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0) return [NameRequired];
        if (trimmed.Length > GroupConstants.MaxFolderNameLength) return [NameTooLong];
        return [];
    }

    /// <summary>
    /// Refusal for a name a sibling already has (same group, same parent; the top level is one
    /// sibling set). The comparison is byte-wise, the unique index's own: case-insensitive names are
    /// worksheet D29, still pending.
    /// </summary>
    public static string DuplicateName(string name) => $"A folder named \"{name}\" already exists here.";

    /// <summary>
    /// Refusal for a delete whose sub-folder would move to the top level next to a folder with the
    /// same name. Nothing is deleted (a default, roadmap decision row 22).
    /// </summary>
    public static string DeleteWouldDuplicate(string folderName, string childName) =>
        $"Can't delete \"{folderName}\": its sub-folder \"{childName}\" would move to the top level, " +
        $"where a folder named \"{childName}\" already exists. Rename one of them first.";

    /// <summary>
    /// The same refusal when the clashing name can no longer be identified (a top-level folder created
    /// and gone again between the delete and the re-check).
    /// </summary>
    public static string DeleteWouldDuplicateAny(string folderName) =>
        $"Can't delete \"{folderName}\": one of its sub-folders would move to the top level next to a " +
        "folder with the same name. Rename one of them first.";
}
