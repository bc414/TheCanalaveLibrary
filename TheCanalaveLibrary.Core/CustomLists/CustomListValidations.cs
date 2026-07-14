namespace TheCanalaveLibrary.Core;

/// <summary>
/// Pure domain validation + naming rules for Custom Lists (Feature 51). No EF dependency — the
/// service layer passes in pre-fetched context (name-exists, current list count, existing-name set)
/// so these methods are unit-testable directly. Mirrors <c>SavedTagSelectionValidations</c>'s
/// "pure rules, service supplies context" shape.
/// </summary>
public static class CustomListValidations
{
    /// <summary>Matches the entity's <c>[MaxLength(256)]</c> — the schema is the source of truth.</summary>
    public const int MaxListNameLength = 256;

    /// <summary>
    /// Abuse-guard ceiling on lists per user (settled 2026-07-13). Applies to creates and clones
    /// alike; entries per list are deliberately uncapped (reads paginate).
    /// </summary>
    public const int MaxListsPerUser = 100;

    /// <summary>
    /// Validates a list name for create/rename and returns validation errors, or an empty list when
    /// valid. <paramref name="nameExists"/> is the caller's pre-fetched "another list of this user
    /// already has this name" check (excluding the row being renamed, when applicable);
    /// <paramref name="listCount"/> is the user's current list count (pass 0 for renames — the cap
    /// only gates operations that add a list).
    /// </summary>
    public static List<string> ValidateListName(string? listName, bool nameExists, int listCount = 0)
    {
        List<string> errors = [];

        if (string.IsNullOrWhiteSpace(listName))
            errors.Add("List name must not be empty.");
        else if (listName.Trim().Length > MaxListNameLength)
            errors.Add($"List name must be {MaxListNameLength} characters or fewer.");

        if (nameExists)
            errors.Add($"You already have a list named \"{listName?.Trim()}\".");

        if (listCount >= MaxListsPerUser)
            errors.Add($"You can have at most {MaxListsPerUser} lists.");

        return errors;
    }

    /// <summary>
    /// Produces a name for a cloned list (<c>CloneListAsync</c>) that doesn't collide with the
    /// cloning user's existing list names: "{ListName} (copy)", then "(copy 2)", "(copy 3)", …
    /// Pure — the caller supplies the full current-name set, built with
    /// <see cref="StringComparer.OrdinalIgnoreCase"/> to match the case-insensitive duplicate rule.
    /// Mirrors <c>SavedTagSelectionValidations.DisambiguateCopyNickname</c>.
    /// </summary>
    public static string DisambiguateCloneName(string sourceListName, HashSet<string> existingNames)
    {
        string candidate = $"{sourceListName} (copy)";
        if (!existingNames.Contains(candidate))
            return Truncate(candidate);

        int suffix = 2;
        while (true)
        {
            candidate = $"{sourceListName} (copy {suffix})";
            if (!existingNames.Contains(candidate))
                return Truncate(candidate);
            suffix++;
        }
    }

    private static string Truncate(string candidate) =>
        candidate.Length > MaxListNameLength ? candidate[..MaxListNameLength] : candidate;
}
