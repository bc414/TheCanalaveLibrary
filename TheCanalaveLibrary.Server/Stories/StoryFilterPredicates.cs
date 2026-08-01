using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// The shared <see cref="StoryFilterDto"/> → <c>IQueryable&lt;Story&gt;</c> predicate: tag include
/// (AND/OR, hierarchy-rolled-up), tag exclude, ship terms, FTS, and viewer-relative interaction
/// exclusions. Extracted from <see cref="ServerStoryReadService"/> by WU-ExploreFilterAxes
/// (2026-07-31) so Manual Tree Search's Explore pane narrows by exactly the same semantics the
/// story-listing surfaces use — a second transcription of tag roll-up and interaction exclusion is
/// precisely the drift this class exists to prevent.
///
/// <para><b>Pure and synchronous</b> (the WU-ApplyFiltersPurity invariant, hidden-deferrals-tracker
/// B12): no <c>DbContext</c>, no I/O, no ambient state. The hierarchy roll-up arrives as the
/// <c>expansion</c> argument and the viewer as <c>viewerId</c>, so every result is a function of its
/// inputs alone — reproducible, loggable, replayable. Callers resolve the map themselves (see
/// <see cref="NamesTagIds"/>, which lets an unfiltered read skip the load entirely).</para>
///
/// <para>Adds no <c>OrderBy</c> and no pagination — those belong to the caller, whose section
/// orderings differ.</para>
/// </summary>
internal static class StoryFilterPredicates
{
    /// <summary>
    /// True when the filter names any tag id at all (include, exclude, or ship member) — i.e. when
    /// the caller actually needs a <see cref="TagExpansionMap"/>. Unfiltered browse pays nothing,
    /// and only a filtered read can trigger a (cached) hierarchy load.
    /// </summary>
    public static bool NamesTagIds(StoryFilterDto f) =>
        f.IncludedTagIds.Count > 0 || f.ExcludedTagIds.Count > 0 ||
        f.IncludedShips.Count > 0 || f.ExcludedShips.Count > 0;

    /// <summary>
    /// Rejects malformed ship criteria before any query work. A ship names at most
    /// <see cref="ShipFilterDto.MaxMembers"/> characters (the predicate builders below are
    /// explicit per arity so the expression stays EF-translatable). Throws the user-facing
    /// <see cref="StoryValidationException"/> so callers translate it to a 400 rather than
    /// logging an unexpected error.
    /// </summary>
    public static void ValidateShipShape(StoryFilterDto filter)
    {
        List<string> errors = [];
        foreach (ShipFilterDto ship in filter.IncludedShips.Concat(filter.ExcludedShips))
        {
            if (ship.MemberTagIds.Count > ShipFilterDto.MaxMembers)
                errors.Add($"A ship filter supports at most {ShipFilterDto.MaxMembers} characters.");
            if (ship.MemberTagIds.Distinct().Count() != ship.MemberTagIds.Count)
                errors.Add("A ship filter cannot name the same character twice.");
        }
        if (errors.Count > 0) throw new StoryValidationException(errors.Distinct().ToList());
    }

    /// <summary>
    /// Applies tag include (AND or OR per <c>filter.IncludeMode</c>), tag exclude (ANY/none), ship
    /// filters, FTS, and viewer-relative interaction exclusions.
    ///
    /// <para>Every tag id — include, exclude, and ship member — expands to {self} ∪ children via
    /// <paramref name="expansion"/> (hierarchy is one level deep). Symmetric: excluding a parent
    /// excludes its children. AND terms are independent: a story tagged only with a child satisfies
    /// a filter naming parent AND child. See layer2-services.md §"Tag Hierarchy Roll-Up".</para>
    /// </summary>
    /// <param name="hasFts">Whether to apply <c>filter.TextQuery</c>. Callers that do not offer a
    /// text axis pass <c>false</c> regardless of the DTO's contents.</param>
    public static IQueryable<Story> ApplyFilters(
        IQueryable<Story> query, StoryFilterDto filter, TagExpansionMap expansion, int? viewerId, bool hasFts)
    {
        // ── Tag include ────────────────────────────────────────────────────────────────────
        // Character tags live in StoryCharacters; all others live in StoryTags. Since every
        // TagId belongs to exactly one entity type, the || always resolves to one side only —
        // this is correct without pre-partitioning the id list.
        if (filter.IncludedTagIds.Count > 0)
        {
            if (filter.IncludeMode == TagIncludeMode.Or)
            {
                // OR — story must match at least one included tag (or child) in either collection.
                int[] anyOf = [.. filter.IncludedTagIds.SelectMany(expansion.Expand).Distinct()];
                query = query.Where(s =>
                    s.StoryCharacters.Any(sc => anyOf.Contains(sc.CharacterTagId)) ||
                    s.StoryTags.Any(st => anyOf.Contains(st.TagId)));
            }
            else
            {
                // AND (default) — story must match every included term; each term is its own
                // {self ∪ children} set with its own subquery, evaluated independently.
                foreach (int tagId in filter.IncludedTagIds)
                {
                    int[] set = expansion.Expand(tagId);
                    query = query.Where(s =>
                        s.StoryCharacters.Any(sc => set.Contains(sc.CharacterTagId)) ||
                        s.StoryTags.Any(st => set.Contains(st.TagId)));
                }
            }
        }

        // ── Tag exclude (story must have none of the excluded tags OR their children) ──
        if (filter.ExcludedTagIds.Count > 0)
        {
            int[] noneOf = [.. filter.ExcludedTagIds.SelectMany(expansion.Expand).Distinct()];
            query = query.Where(s =>
                !s.StoryCharacters.Any(sc => noneOf.Contains(sc.CharacterTagId)) &&
                !s.StoryTags.Any(st => noneOf.Contains(st.TagId)));
        }

        // ── Ship filters (WU-TagFanon) — AND across ships; each ship needs ONE pairing whose
        //    member set covers every named character (roll-up applied per member). ──
        foreach (ShipFilterDto ship in filter.IncludedShips)
            query = ApplyShipTerm(query, ship, expansion, negate: false);
        foreach (ShipFilterDto ship in filter.ExcludedShips)
            query = ApplyShipTerm(query, ship, expansion, negate: true);

        // ── FTS ───────────────────────────────────────────────────────────────────────────
        if (hasFts)
        {
            string textQuery = filter.TextQuery!;
            // PlainToTsQuery is safer than ToTsQuery (no tsquery syntax knowledge required from callers).
            // SearchVector is a shadow property on StoryListing; EF.Property accesses it in a subquery.
            query = query.Where(s => s.StoryListing != null &&
                EF.Property<NpgsqlTsVector>(s.StoryListing, "SearchVector")
                    .Matches(EF.Functions.PlainToTsQuery("english", textQuery)));
        }

        // ── Interaction exclusions (authenticated viewer only) ────────────────────────────
        if (filter.ExcludedInteractions.Count > 0 && viewerId.HasValue)
        {
            int viewerIdValue = viewerId.Value;

            bool exclFav    = filter.ExcludedInteractions.Contains(UserStoryInteractionTypeEnum.Favorite);
            bool exclHidden = filter.ExcludedInteractions.Contains(UserStoryInteractionTypeEnum.PrivateFavorite);
            bool exclFollow = filter.ExcludedInteractions.Contains(UserStoryInteractionTypeEnum.Follow);
            bool exclComp   = filter.ExcludedInteractions.Contains(UserStoryInteractionTypeEnum.Complete);
            bool exclLater  = filter.ExcludedInteractions.Contains(UserStoryInteractionTypeEnum.ReadLater);
            bool exclIgnore = filter.ExcludedInteractions.Contains(UserStoryInteractionTypeEnum.Ignore);

            // Exclude stories where the viewer's USI row has any excluded bit set.
            // The constants (exclFav, etc.) are evaluated at query-compilation time and fold into the
            // SQL as literal true/false, which Postgres optimises away. Zero SQL overhead for bits that
            // aren't excluded.
            query = query.Where(s => !s.UserStoryInteractions
                .Any(usi => usi.UserId == viewerIdValue &&
                    (exclFav    && usi.IsFavorite      ||
                     exclHidden && usi.IsHiddenFavorite ||
                     exclFollow && usi.IsFollowed       ||
                     exclComp   && usi.IsCompleted      ||
                     exclLater  && usi.IsReadItLater    ||
                     exclIgnore && usi.IsIgnored)));
        }

        return query;
    }

    /// <summary>
    /// One ship term: the story must (or, negated, must not) contain a single pairing whose
    /// member set covers every named character — each member id already roll-up-expanded.
    /// Members beyond <see cref="ShipFilterDto.MaxMembers"/> are rejected, not silently capped.
    /// Explicit 1/2/3-member branches keep the predicate EF-translatable without expression-tree
    /// assembly.
    /// </summary>
    private static IQueryable<Story> ApplyShipTerm(
        IQueryable<Story> query, ShipFilterDto ship, TagExpansionMap expansion, bool negate)
    {
        // Arity is already validated by ValidateShipShape at the entry point.
        List<int[]> sets = ship.MemberTagIds.Select(expansion.Expand).ToList();
        if (sets.Count == 0) return query;

        CharacterPairingType? type = ship.PairingType;
        Expression<Func<Story, bool>> predicate = sets.Count switch
        {
            1 => Ship1(sets[0], type),
            2 => Ship2(sets[0], sets[1], type),
            _ => Ship3(sets[0], sets[1], sets[2], type),
        };

        return negate
            ? query.Where(Not(predicate))
            : query.Where(predicate);
    }

    /// <summary>Logical negation of a predicate expression (EF translates NOT(EXISTS…) fine).</summary>
    private static Expression<Func<T, bool>> Not<T>(Expression<Func<T, bool>> expr) =>
        Expression.Lambda<Func<T, bool>>(Expression.Not(expr.Body), expr.Parameters);

    private static Expression<Func<Story, bool>> Ship1(int[] a, CharacterPairingType? type) =>
        s => s.StoryCharacterPairings.Any(p =>
            (type == null || p.PairingType == type) &&
            p.Members.Any(m => a.Contains(m.StoryCharacter.CharacterTagId)));

    private static Expression<Func<Story, bool>> Ship2(int[] a, int[] b, CharacterPairingType? type) =>
        s => s.StoryCharacterPairings.Any(p =>
            (type == null || p.PairingType == type) &&
            p.Members.Any(m => a.Contains(m.StoryCharacter.CharacterTagId)) &&
            p.Members.Any(m => b.Contains(m.StoryCharacter.CharacterTagId)));

    private static Expression<Func<Story, bool>> Ship3(int[] a, int[] b, int[] c, CharacterPairingType? type) =>
        s => s.StoryCharacterPairings.Any(p =>
            (type == null || p.PairingType == type) &&
            p.Members.Any(m => a.Contains(m.StoryCharacter.CharacterTagId)) &&
            p.Members.Any(m => b.Contains(m.StoryCharacter.CharacterTagId)) &&
            p.Members.Any(m => c.Contains(m.StoryCharacter.CharacterTagId)));
}
