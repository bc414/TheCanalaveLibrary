namespace TheCanalaveLibrary.Core;

/// <summary>
/// The six mutable interaction types a user can toggle on a story from the panel.
/// Declaration order is the canonical left-to-right button order:
/// Favorite → PrivateFavorite → Follow → Complete → ReadLater → Ignore.
/// </summary>
public enum InteractionTypeEnum
{
    Favorite,
    PrivateFavorite,
    Follow,
    Complete,
    ReadLater,
    Ignore
}
