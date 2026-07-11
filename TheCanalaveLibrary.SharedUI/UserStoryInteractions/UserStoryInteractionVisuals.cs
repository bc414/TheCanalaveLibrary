using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.SharedUI;

/// <summary>
/// The locked icon/label mapping from audit/UserStoryInteractions.md (2026-06-22), with
/// AccentColor carried as var(--color-interaction-*) references since the Phase A token lock
/// (2026-07-10) — the actual color VALUES live in app.css @theme (single source; they follow
/// theme overrides and the contrast tooling). Do not reintroduce hex here; change the token.
///
/// PrivateFavorite reuses Favorite's IconPath: color alone signals privacy.
/// All paths use the default SVG nonzero fill rule; winding direction drives cutouts.
/// </summary>
public static class UserStoryInteractionVisuals
{
    public readonly record struct Info(string IconPath, string AccentColor, string Label);

    private const string HeartPath =
        "M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z";

    private static readonly IReadOnlyDictionary<UserStoryInteractionTypeEnum, Info> Map =
        new Dictionary<UserStoryInteractionTypeEnum, Info>
        {
            [UserStoryInteractionTypeEnum.Favorite] = new(HeartPath, "var(--color-interaction-favorite)", "Favorite"),
            [UserStoryInteractionTypeEnum.PrivateFavorite] = new(HeartPath, "var(--color-interaction-private)", "Private Favorite"),
            [UserStoryInteractionTypeEnum.Follow] = new(
                "M6 8A6 6 0 0 1 18 8A6 6 0 0 1 6 8Z M9 14L6 22L9.5 20L12 21.5L14.5 20L18 22L15 14Z",
                "var(--color-interaction-follow)", // orange since the gate (was Manaphy Teal; conflicted with curation greens)
                "Following"),
            [UserStoryInteractionTypeEnum.Complete] = new(
                "M12 2A10 10 0 0 1 22 12A10 10 0 0 1 12 22A10 10 0 0 1 2 12A10 10 0 0 1 12 2Z M6 12.5L5 14L10 19L20 7L18.5 5.5L10 16Z",
                "var(--color-interaction-complete)",
                "Completed"),
            [UserStoryInteractionTypeEnum.ReadLater] = new(
                "M5 16A7 7 0 0 1 19 16Z M5 14A7 7 0 0 0 19 14Z M10.5 15A1.5 1.5 0 0 1 13.5 15A1.5 1.5 0 0 1 10.5 15Z M9 1L15 1L15 2L9 2Z M9 3L15 3L15 4L9 4Z M9 5L15 5L15 6L9 6Z",
                "var(--color-interaction-readlater)",
                "Read It Later"),
            [UserStoryInteractionTypeEnum.Ignore] = new(
                "M12 2A10 10 0 0 1 22 12A10 10 0 0 1 12 22A10 10 0 0 1 2 12A10 10 0 0 1 12 2Z M5 12A7 7 0 0 0 19 12A7 7 0 0 0 5 12Z M5.5 7.5L7.5 5.5L18.5 16.5L16.5 18.5Z",
                "var(--color-interaction-ignore)",
                "Ignored"),
        };

    public static Info For(UserStoryInteractionTypeEnum type) => Map[type];
}
