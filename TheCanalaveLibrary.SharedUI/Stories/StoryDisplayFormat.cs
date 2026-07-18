using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.SharedUI;

/// <summary>
/// The single source for story display formatting — word-count string, status label, rating
/// label (MA-209). Replaces the switch tables formerly triplicated across
/// StoryCard/StoryDesktop/StoryMobile; the badge CLASS half of those tables already lives in
/// <see cref="StatusBadges"/> (labels here, tint recipes there).
///
/// Behavior is preserved exactly, including the known rounding quirk at the K→M boundary:
/// 999,999 words formats as "1000K words" (999.999 rounds to 1000 under F0), not "1.0M words".
/// </summary>
public static class StoryDisplayFormat
{
    public static string WordCount(int wordCount) => wordCount switch
    {
        < 1_000     => $"{wordCount} words",
        < 1_000_000 => $"{wordCount / 1000.0:F0}K words",
        _           => $"{wordCount / 1_000_000.0:F1}M words"
    };

    public static string StatusLabel(StoryStatusEnum status) => status switch
    {
        StoryStatusEnum.Draft           => "Draft",
        StoryStatusEnum.PendingApproval => "Pending Approval",
        StoryStatusEnum.InProgress      => "In Progress",
        StoryStatusEnum.Completed       => "Complete",
        StoryStatusEnum.OnHiatus        => "On Hiatus",
        StoryStatusEnum.Cancelled       => "Cancelled",
        StoryStatusEnum.Rewriting       => "Rewriting",
        StoryStatusEnum.OpenBeta        => "Open Beta",
        StoryStatusEnum.Rejected        => "Rejected",
        _                               => "Unknown"
    };

    public static string RatingLabel(Rating rating) => rating switch
    {
        Rating.E => "Everyone",
        Rating.T => "Teen",
        Rating.M => "Mature",
        _        => "Unknown"
    };
}
