namespace TheCanalaveLibrary.Core;

/// <summary>
/// Self-scoped management of the signed-in member's durable mature-content reveals
/// (WU-AccessGate; the "/settings" → "Mature content you've revealed" section). Single
/// read+write interface — self-referential settings surface, same shape rationale as
/// <see cref="IUserSettingsService"/>. Anonymous viewers manage reveals by clearing the prefs
/// cookie instead; every method here no-ops/returns empty for them.
/// </summary>
public interface IContentRevealService
{
    /// <summary>The caller's reveals, newest first, with display titles resolved from ground
    /// truth (Personal plane — the member consented to these; titles are never re-filtered).</summary>
    Task<IReadOnlyList<RevealDisplayDto>> GetMyRevealsAsync();

    /// <summary>Removes one reveal — the item gates again on the caller's next visit.</summary>
    Task RemoveAsync(RevealedEntityType entityType, int entityId);
}

/// <summary>One row of the settings reveal list.</summary>
public sealed record RevealDisplayDto(
    RevealedEntityType EntityType,
    int EntityId,
    string Title,
    DateTime DateRevealed);
