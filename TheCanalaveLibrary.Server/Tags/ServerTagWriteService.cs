using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

public class ServerTagWriteService(
    ApplicationDbContext db,
    ReadOnlyApplicationDbContext readDb,
    ISpriteReadService spriteReadService,
    IActiveUserContext activeUser)
    : ServerTagReadService(readDb, spriteReadService, activeUser), ITagWriteService
{
    public async Task<int> CreateTagAsync(CreateTagDto dto)
    {
        RequireMod();

        bool nameExists = await db.Tags
            .AnyAsync(t => t.TagName.ToLower() == dto.TagName.ToLower() && t.TagTypeId == dto.TagTypeId);

        Tag? parentTag = dto.ParentTagId is null
            ? null
            : await db.Tags.FindAsync(dto.ParentTagId.Value);

        TagValidations.ValidateCreate(dto, nameExists, parentTag);

        var tag = new Tag
        {
            TagName = dto.TagName.Trim(),
            TagTypeId = dto.TagTypeId,
            Description = dto.Description?.Trim(),
            SpriteIdentifier = dto.SpriteIdentifier?.Trim(),
            IsFanon = dto.IsFanon,
            AllowOCDetails = TagValidations.CoerceAllowOCDetails(dto.AllowOCDetails, dto.TagTypeId),
            ParentTagId = dto.ParentTagId
        };

        db.Tags.Add(tag);
        await db.SaveChangesAsync();
        return tag.TagId;
    }

    public async Task UpdateTagAsync(UpdateTagDto dto)
    {
        RequireMod();

        var tag = await db.Tags.FindAsync(dto.TagId)
            ?? throw new KeyNotFoundException($"Tag {dto.TagId} not found.");

        bool nameExists = await db.Tags
            .AnyAsync(t => t.TagId != dto.TagId
                           && t.TagName.ToLower() == dto.TagName.ToLower()
                           && t.TagTypeId == dto.TagTypeId);

        Tag? parentTag = dto.ParentTagId is null
            ? null
            : await db.Tags.FindAsync(dto.ParentTagId.Value);

        TagValidations.ValidateUpdate(dto, nameExists, parentTag);

        tag.TagName = dto.TagName.Trim();
        tag.TagTypeId = dto.TagTypeId;
        tag.Description = dto.Description?.Trim();
        tag.SpriteIdentifier = dto.SpriteIdentifier?.Trim();
        tag.IsFanon = dto.IsFanon;
        tag.AllowOCDetails = TagValidations.CoerceAllowOCDetails(dto.AllowOCDetails, dto.TagTypeId);
        tag.ParentTagId = dto.ParentTagId;

        await db.SaveChangesAsync();
    }

    public async Task DeleteTagAsync(int tagId)
    {
        RequireMod();

        var tag = await db.Tags.FindAsync(tagId)
            ?? throw new KeyNotFoundException($"Tag {tagId} not found.");

        // Block deletion if the tag is referenced anywhere — prevents Restrict FK violations.
        int storyTagCount = await db.StoryTags.CountAsync(st => st.TagId == tagId);
        int selectionEntryCount = await db.SavedTagSelectionEntries.CountAsync(e => e.TagId == tagId);
        int childCount = await db.Tags.CountAsync(t => t.ParentTagId == tagId);

        int totalReferences = storyTagCount + selectionEntryCount + childCount;
        if (totalReferences > 0)
        {
            List<string> parts = [];
            if (storyTagCount > 0) parts.Add($"{storyTagCount} {(storyTagCount == 1 ? "story" : "stories")}");
            if (selectionEntryCount > 0) parts.Add($"{selectionEntryCount} saved selection{(selectionEntryCount == 1 ? "" : "s")}");
            if (childCount > 0) parts.Add($"{childCount} child tag{(childCount == 1 ? "" : "s")}");
            throw new TagValidationException(
                $"Cannot delete \"{tag.TagName}\" — it is referenced by {string.Join(", ", parts)}.");
        }

        db.Tags.Remove(tag);
        await db.SaveChangesAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RequireMod()
    {
        if (!activeUser.IsModerator && !activeUser.IsAdmin)
            throw new UnauthorizedAccessException("Tag administration requires moderator or admin role.");
    }
}
