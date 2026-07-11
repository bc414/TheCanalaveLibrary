namespace TheCanalaveLibrary.Core;

/// <summary>
/// Pure domain validation + naming rules for Saved Tag Selections (WU43). No EF dependency — the
/// service layer passes in pre-fetched values (nickname-exists, current nickname set) so these methods
/// are unit-testable directly. Mirrors <c>TagValidations</c>'s "pure rules, service supplies context"
/// shape.
/// </summary>
public static class SavedTagSelectionValidations
{
    public const int MaxNicknameLength = 100;
    public const int MaxDescriptionLength = 280;

    /// <summary>
    /// Validates a <see cref="SavedTagSelectionInput"/> and returns validation errors, or an empty
    /// list when valid. <paramref name="nicknameExists"/> is the caller's pre-fetched
    /// "another selection of this user already has this nickname" check (excluding the row being
    /// updated, when applicable).
    /// </summary>
    public static List<string> CanSave(this SavedTagSelectionInput dto, bool nicknameExists)
    {
        List<string> errors = [];

        if (string.IsNullOrWhiteSpace(dto.Nickname))
            errors.Add("Nickname must not be empty.");
        else if (dto.Nickname.Trim().Length > MaxNicknameLength)
            errors.Add($"Nickname must be {MaxNicknameLength} characters or fewer.");

        if (dto.Description is { Length: > MaxDescriptionLength })
            errors.Add($"Description must be {MaxDescriptionLength} characters or fewer.");

        if (dto.IncludedTagIds.Count == 0 && dto.ExcludedTagIds.Count == 0)
            errors.Add("A saved selection must include or exclude at least one tag.");

        if (nicknameExists)
            errors.Add($"You already have a saved selection named \"{dto.Nickname.Trim()}\".");

        return errors;
    }

    /// <summary>
    /// Produces a nickname for a copy-on-write share (<c>CopyPublicSelectionAsync</c>) that doesn't
    /// collide with the copying user's existing nicknames: "{Nickname} (copy)", then "(copy 2)",
    /// "(copy 3)", … Pure — the caller supplies the full current-nickname set, built with
    /// <see cref="StringComparer.OrdinalIgnoreCase"/> to match the DB's unique-index semantics.
    /// </summary>
    public static string DisambiguateCopyNickname(string sourceNickname, HashSet<string> existingNicknames)
    {
        string candidate = $"{sourceNickname} (copy)";
        if (!existingNicknames.Contains(candidate))
            return Truncate(candidate);

        int suffix = 2;
        while (true)
        {
            candidate = $"{sourceNickname} (copy {suffix})";
            if (!existingNicknames.Contains(candidate))
                return Truncate(candidate);
            suffix++;
        }
    }

    private static string Truncate(string candidate) =>
        candidate.Length > MaxNicknameLength ? candidate[..MaxNicknameLength] : candidate;
}
