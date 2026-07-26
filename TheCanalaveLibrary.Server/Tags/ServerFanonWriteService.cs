using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Write side of the fanonization pipeline (WU-TagFanon Groups 7+8).
///
/// <para><b>Moderator half:</b> <see cref="LinkGroupAsync"/> points a custom-name group at an
/// official tag (fanon or canon) and invites the affected authors; <see cref="NotifyNewAuthorsAsync"/>
/// re-invites newly-arrived authors. The never-twice-per-(author, tag) rule is enforced here via
/// <see cref="TagAdoptionState.DateNotified"/> — deliberately NOT via notification unread-dedup,
/// which would re-fire on anyone who had read and moved on.</para>
///
/// <para><b>Author half:</b> adoption is always opt-in, never automatic. It mutates rows in
/// place — naming moves to the tag (IsOc→false, CustomName→null), Nuance and priority survive,
/// and a character row keeps its stable StoryCharacterId so pairings survive. A story already
/// carrying the target tag skips with an explanation (merging would re-point pairing members).
/// Notification is fired best-effort post-commit (layer2-services.md §"Notification Generation").</para>
/// </summary>
public class ServerFanonWriteService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser,
    ISiteSettingsReadService siteSettings,
    INotificationWriteService notifications,
    ILogger<ServerFanonWriteService> logger)
    : ServerFanonReadService(readDbFactory, activeUser, siteSettings), IFanonWriteService
{
    public async Task<int> LinkGroupAsync(FanonLinkCreateDto dto)
    {
        RequireMod();

        string normalized = Normalize(dto.Name);
        if (normalized.Length == 0)
            throw new TagValidationException("A fanon link needs a non-empty group name.");

        Tag baseTag = await writeDb.Tags.FindAsync(dto.BaseTagId)
            ?? throw new KeyNotFoundException($"Base tag {dto.BaseTagId} not found.");
        Tag targetTag = await writeDb.Tags.FindAsync(dto.TargetTagId)
            ?? throw new KeyNotFoundException($"Target tag {dto.TargetTagId} not found.");
        if (baseTag.TagId == targetTag.TagId)
            throw new TagValidationException("A group cannot be linked to its own base tag.");

        bool exists = await writeDb.FanonLinks
            .AnyAsync(l => l.NormalizedName == normalized && l.BaseTagId == dto.BaseTagId);
        if (exists)
            throw new TagValidationException($"“{dto.Name}” on {baseTag.TagName} is already linked.");

        FanonLink link = new()
        {
            NormalizedName = normalized,
            BaseTagId = dto.BaseTagId,
            TargetTagId = dto.TargetTagId,
            LinkedByUserId = ActiveUser.UserId,
            DateLinked = DateTime.UtcNow
        };
        writeDb.FanonLinks.Add(link);
        await writeDb.SaveChangesAsync();

        return await NotifyNewAuthorsCoreAsync(link, baseTag.TagTypeId);
    }

    public async Task<int> NotifyNewAuthorsAsync(string name, int baseTagId)
    {
        RequireMod();

        string normalized = Normalize(name);
        FanonLink link = await writeDb.FanonLinks
                             .Include(l => l.BaseTag)
                             .FirstOrDefaultAsync(l => l.NormalizedName == normalized && l.BaseTagId == baseTagId)
                         ?? throw new KeyNotFoundException($"No fanon link exists for “{name}”.");

        return await NotifyNewAuthorsCoreAsync(link, link.BaseTag.TagTypeId);
    }

    public async Task<AdoptResultDto> AdoptAsync(int targetTagId, int storyId)
    {
        if (ActiveUser.UserId is not int authorId)
            throw new InvalidOperationException("Adopting a tag requires an authenticated user.");
        return await AdoptCoreAsync(targetTagId, authorId, storyId);
    }

    public async Task<AdoptResultDto> AdoptAllAsync(int targetTagId)
    {
        if (ActiveUser.UserId is not int authorId)
            throw new InvalidOperationException("Adopting a tag requires an authenticated user.");
        return await AdoptCoreAsync(targetTagId, authorId, storyId: null);
    }

    public async Task SetDismissedAsync(int targetTagId, bool dismissed)
    {
        if (ActiveUser.UserId is not int authorId)
            throw new InvalidOperationException("Dismissing a tag adoption requires an authenticated user.");

        TagAdoptionState? state = await writeDb.TagAdoptionStates.FindAsync(authorId, targetTagId);
        if (state is null)
        {
            state = new TagAdoptionState { UserId = authorId, TargetTagId = targetTagId };
            writeDb.TagAdoptionStates.Add(state);
        }
        state.IsDismissed = dismissed;
        await writeDb.SaveChangesAsync();
    }

    // ── Internals ─────────────────────────────────────────────────────────────────

    private void RequireMod()
    {
        if (!ActiveUser.IsModerator && !ActiveUser.IsAdmin)
            throw new UnauthorizedAccessException("Fanonization requires moderator or admin role.");
    }

    /// <summary>
    /// Invite every author of the group's matching rows who has never been told about the target
    /// tag. Drafts count for notification (personal plane) though not for public reach; taken-down
    /// stories are excluded (the IsTakenDown filter stays active); rows with no author are skipped.
    /// </summary>
    private async Task<int> NotifyNewAuthorsCoreAsync(FanonLink link, TagTypeEnum axis)
    {
        List<int> authorIds = axis == TagTypeEnum.Character
            ? await writeDb.StoryCharacters
                .IgnoreQueryFilters(["ContentRating"])
                .Where(sc => sc.CharacterTagId == link.BaseTagId
                    && sc.CustomName != null
                    && sc.CustomName!.ToLower().Trim() == link.NormalizedName
                    && sc.Story.AuthorId != null)
                .Select(sc => sc.Story.AuthorId!.Value)
                .Distinct()
                .ToListAsync()
            : await writeDb.StoryTags
                .IgnoreQueryFilters(["ContentRating"])
                .Where(st => st.TagId == link.BaseTagId
                    && st.CustomName != null
                    && st.CustomName!.ToLower().Trim() == link.NormalizedName
                    && st.Story.AuthorId != null)
                .Select(st => st.Story.AuthorId!.Value)
                .Distinct()
                .ToListAsync();

        if (authorIds.Count == 0) return 0;

        // Never notify the same author twice for the same tag — DateNotified is the record.
        HashSet<int> alreadyTold = (await writeDb.TagAdoptionStates
                .Where(s => s.TargetTagId == link.TargetTagId && s.DateNotified != null
                            && authorIds.Contains(s.UserId))
                .Select(s => s.UserId)
                .ToListAsync())
            .ToHashSet();
        List<int> fresh = authorIds.Where(a => !alreadyTold.Contains(a)).ToList();
        if (fresh.Count == 0) return 0;

        DateTime now = DateTime.UtcNow;
        Dictionary<int, TagAdoptionState> existing = await writeDb.TagAdoptionStates
            .Where(s => s.TargetTagId == link.TargetTagId && fresh.Contains(s.UserId))
            .ToDictionaryAsync(s => s.UserId);
        foreach (int authorId in fresh)
        {
            if (existing.TryGetValue(authorId, out TagAdoptionState? state))
                state.DateNotified = now;
            else
                writeDb.TagAdoptionStates.Add(new TagAdoptionState
                {
                    UserId = authorId, TargetTagId = link.TargetTagId, DateNotified = now
                });
        }
        await writeDb.SaveChangesAsync();

        // Best-effort post-commit (layer2-services.md §"Notification Generation").
        try
        {
            await notifications.NotifyTagAdoptionSuggestedAsync(
                fresh, link.TargetTagId, ActiveUser.UserId ?? 0);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Tag-adoption notification failed for link {FanonLinkId} (target tag {TargetTagId})",
                link.FanonLinkId, link.TargetTagId);
        }

        return fresh.Count;
    }

    /// <summary>
    /// The adoption mutation. In-place on <c>StoryCharacter</c> (stable id → pairings survive);
    /// delete+insert on <c>StoryTag</c> (composite PK cannot be updated) with the overlay carried
    /// across minus the custom name. Collisions skip, never merge.
    /// </summary>
    private async Task<AdoptResultDto> AdoptCoreAsync(int targetTagId, int authorId, int? storyId)
    {
        var links = await writeDb.FanonLinks
            .Where(l => l.TargetTagId == targetTagId)
            .Select(l => new { l.NormalizedName, l.BaseTagId, BaseTypeId = l.BaseTag.TagTypeId })
            .ToListAsync();
        if (links.Count == 0)
            throw new KeyNotFoundException($"No fanon link targets tag {targetTagId}.");

        int adopted = 0, skipped = 0;
        foreach (var link in links)
        {
            if (link.BaseTypeId == TagTypeEnum.Character)
            {
                List<StoryCharacter> mine = await writeDb.StoryCharacters
                    .IgnoreQueryFilters(["ContentRating"])
                    .Where(sc => sc.Story.AuthorId == authorId
                        && sc.CharacterTagId == link.BaseTagId
                        && sc.CustomName != null
                        && sc.CustomName!.ToLower().Trim() == link.NormalizedName
                        && (storyId == null || sc.StoryId == storyId))
                    .ToListAsync();
                if (mine.Count == 0) continue;

                List<int> storyIds = mine.Select(m => m.StoryId).Distinct().ToList();
                HashSet<int> colliding = (await writeDb.StoryCharacters
                        .Where(sc => storyIds.Contains(sc.StoryId) && sc.CharacterTagId == targetTagId)
                        .Select(sc => sc.StoryId)
                        .ToListAsync())
                    .ToHashSet();

                // Intra-story case-variant duplicates ("Saura" AND "saura" on one base tag) are
                // legal under the case-SENSITIVE DB index but both match this case-INSENSITIVE
                // group — adopting both would produce two identical (story, target, NULL) rows and
                // violate the unique index as a raw DbUpdateException. Treat the whole story as a
                // collision: skip it, explain it, let the author de-duplicate and re-adopt. Writes
                // can no longer create these (ValidateStructuredTagGatesAsync compares case-
                // insensitively); this handles rows predating that rule.
                foreach (int dupStoryId in mine.GroupBy(m => m.StoryId).Where(g => g.Count() > 1).Select(g => g.Key))
                    colliding.Add(dupStoryId);

                foreach (StoryCharacter row in mine)
                {
                    if (colliding.Contains(row.StoryId)) { skipped++; continue; }
                    // Naming moves to the tag; the note and priority survive; the row id is
                    // stable so pairing memberships survive (WU-TagFanon 8.6).
                    row.CharacterTagId = targetTagId;
                    row.IsOc = false;
                    row.CustomName = null;
                    adopted++;
                }
            }
            else
            {
                List<StoryTag> mine = await writeDb.StoryTags
                    .IgnoreQueryFilters(["ContentRating"])
                    .Where(st => st.Story.AuthorId == authorId
                        && st.TagId == link.BaseTagId
                        && st.CustomName != null
                        && st.CustomName!.ToLower().Trim() == link.NormalizedName
                        && (storyId == null || st.StoryId == storyId))
                    .ToListAsync();
                if (mine.Count == 0) continue;

                List<int> storyIds = mine.Select(m => m.StoryId).Distinct().ToList();
                HashSet<int> colliding = (await writeDb.StoryTags
                        .Where(st => storyIds.Contains(st.StoryId) && st.TagId == targetTagId)
                        .Select(st => st.StoryId)
                        .ToListAsync())
                    .ToHashSet();

                // Flat rows can't case-collide within a story (StoryTag's PK is (StoryId, TagId),
                // so one row per tag), but keep the guard symmetric and defensive.
                foreach (int dupStoryId in mine.GroupBy(m => m.StoryId).Where(g => g.Count() > 1).Select(g => g.Key))
                    colliding.Add(dupStoryId);

                foreach (StoryTag row in mine)
                {
                    if (colliding.Contains(row.StoryId)) { skipped++; continue; }
                    writeDb.StoryTags.Remove(row);
                    writeDb.StoryTags.Add(new StoryTag
                    {
                        StoryId = row.StoryId,
                        TagId = targetTagId,
                        Priority = row.Priority,
                        CustomName = null,
                        Nuance = row.Nuance
                    });
                    adopted++;
                }
            }
        }

        await writeDb.SaveChangesAsync();
        return new AdoptResultDto(adopted, skipped);
    }
}
