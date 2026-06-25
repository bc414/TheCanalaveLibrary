namespace TheCanalaveLibrary.Core;

/// <summary>
/// Pure static mapper between the UI/write-boundary <see cref="GroupAudienceType"/> preset and
/// the persisted <c>(AudienceRating, MaxContentRating)</c> pair on <see cref="Group"/>.
/// Settled WU32 — see <c>cross-cutting.md</c> §"Group Audience-Visibility Filter".
/// </summary>
/// <remarks>
/// The preset is NOT stored in the database. The DB stores only the two <see cref="Rating"/>
/// columns. This mapper is the single source of truth for the correspondence table:
/// <list type="table">
///   <listheader><term>Preset</term><term>AudienceRating</term><term>MaxContentRating</term></listheader>
///   <item><term>Standard</term><term>E</term><term>M</term></item>
///   <item><term>SfwOnly</term><term>E</term><term>T</term></item>
///   <item><term>Mature</term><term>M</term><term>M</term></item>
/// </list>
/// </remarks>
public static class GroupAudienceTypeMapper
{
    /// <summary>
    /// Returns the <c>(AudienceRating, MaxContentRating)</c> pair for a given preset. Used by
    /// <c>ServerGroupWriteService.CreateGroupAsync</c> to stamp the entity from the DTO preset.
    /// </summary>
    public static (Rating AudienceRating, Rating MaxContentRating) ToRatings(GroupAudienceType type) =>
        type switch
        {
            GroupAudienceType.Standard => (Rating.E, Rating.M),
            GroupAudienceType.SfwOnly  => (Rating.E, Rating.T),
            GroupAudienceType.Mature   => (Rating.M, Rating.M),
            _                          => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

    /// <summary>
    /// Derives the display preset from the persisted ratings. Used by the read service to project
    /// a <see cref="GroupAudienceType"/> for the <see cref="GroupCardDto"/> / <see cref="GroupDetailDto"/>
    /// audience badge.
    /// </summary>
    public static GroupAudienceType FromRatings(Rating audienceRating, Rating maxContentRating) =>
        (audienceRating, maxContentRating) switch
        {
            (Rating.E, Rating.T) => GroupAudienceType.SfwOnly,
            (Rating.M, _)        => GroupAudienceType.Mature,
            _                    => GroupAudienceType.Standard   // (E, M) and any unrecognised pair → Standard
        };
}
