namespace TheCanalaveLibrary.Core;

/// <summary>
/// Provides static extension methods for mapping between different story representations.
/// </summary>
public static class StoryMappers
{
    public static StoryTagDTO ToStoryTagDTO(this IStoryTag storyTag)
    {
        return new StoryTagDTO
        {
            TagId = storyTag.TagId,
            Priority = storyTag.Priority,
            TagTypeEnum = storyTag.TagTypeEnum
        };
    }

    public static StoryTag ToStoryTag(this IStoryTag tempStoryTag)
    {
        return new StoryTag
        {
            TagId = tempStoryTag.TagId,
            Priority = tempStoryTag.Priority,
        };
    }

    /// <summary>
    /// Maps any object implementing IStoryProperties to a StoryEditDTO.
    /// </summary>
    public static StoryUpdateDTO ToStoryUpdateDTO(this IEditableStoryProperties story, int storyId)
    {
        return new StoryUpdateDTO
        {
            StoryId = storyId,
            Title = story.Title,
            ShortDescription = story.ShortDescription,
            Rating = story.Rating,
            StoryStatusId = story.StoryStatusId,
            CoverArtRelativeUrl = story.CoverArtRelativeUrl,
            LongDescription = story.LongDescription,
            PostApprovalStatus = story.PostApprovalStatus,
                StoryTags = new List<IStoryTag>(story.StoryTags),
            StoryCharacters = new List<StoryCharacterDto>(story.StoryCharacters),
            SettingDetails = new List<SettingDetailDto>(story.SettingDetails),
            StoryCharacterPairings = new List<StoryCharacterPairingDto>(story.StoryCharacterPairings)
        };
    }

    /// <summary>
    /// Maps any object implementing IStoryProperties to a CreateStoryDTO.
    /// AuthorId is omitted — the server service stamps it from IActiveUserContext.UserId.
    /// </summary>
    public static CreateStoryDTO ToCreateStoryDTO(this IEditableStoryProperties story)
    {
        return new CreateStoryDTO
        {
            Title = story.Title,
            ShortDescription = story.ShortDescription,
            Rating = story.Rating,
            StoryStatusId = story.StoryStatusId,
            CoverArtRelativeUrl = story.CoverArtRelativeUrl,
            LongDescription = story.LongDescription,
            PostApprovalStatus = story.PostApprovalStatus,
            StoryTags = new List<IStoryTag>(story.StoryTags),
            StoryCharacters = new List<StoryCharacterDto>(story.StoryCharacters),
            SettingDetails = new List<SettingDetailDto>(story.SettingDetails),
            StoryCharacterPairings = new List<StoryCharacterPairingDto>(story.StoryCharacterPairings)
        };
    }

    public static Story ToStory(this IEditableStoryProperties tempStory)
    {
        // Settled WU12 fix: a bare `new Story()` leaves StoryListing/StoryDetail null! — the very next
        // line (UpdateStoryEditableProperties) dereferences both, so this threw an NRE on every create.
        // Both partitions must exist before mapping into them.
        Story actualStory = new Story
        {
            StoryListing = new StoryListing(),
            StoryDetail = new StoryDetail()
        };
        return actualStory.UpdateStoryEditableProperties(tempStory);
    }

    public static Story UpdateStoryEditableProperties(this Story actualStory, IEditableStoryProperties tempStory)
    {
        actualStory.StoryListing.StoryTitle = tempStory.Title;
        actualStory.StoryListing.ShortDescription = tempStory.ShortDescription;
        actualStory.Rating = tempStory.Rating;
        actualStory.StoryStatusId = tempStory.StoryStatusId;
        actualStory.StoryListing.CoverArtRelativeUrl = tempStory.CoverArtRelativeUrl;
        actualStory.StoryDetail.LongDescription = tempStory.LongDescription;
        actualStory.StoryDetail.PostApprovalStatus = tempStory.PostApprovalStatus;

        // ── StoryTags (Genre / ContentWarning / CrossoverFandom / Setting flat rows) ──
        // ContentWarning gets no priority picker — server coerces it to Primary regardless.
        actualStory.StoryTags.Clear();
        foreach (IStoryTag tempTag in tempStory.StoryTags)
        {
            StoryTag st = tempTag.ToStoryTag();
            if (tempTag.TagTypeEnum == TagTypeEnum.ContentWarning)
                st.Priority = TagPriority.Primary;
            actualStory.StoryTags.Add(st);
        }

        // ── StoryCharacters ────────────────────────────────────────────────────────
        // Clear pairings first so their Members cascade-delete before StoryCharacter rows go.
        actualStory.StoryCharacterPairings.Clear();
        actualStory.StoryCharacters.Clear();
        foreach (StoryCharacterDto charDto in tempStory.StoryCharacters)
        {
            actualStory.StoryCharacters.Add(new StoryCharacter
            {
                CharacterTagId = charDto.CharacterTagId,
                Priority       = charDto.Priority,
                IsOc           = charDto.IsOc,
                OcName         = charDto.OcName,
                OcBio          = charDto.OcBio
            });
        }

        // ── StoryCharacterPairings (members reference the rebuilt StoryCharacter objects) ──
        foreach (StoryCharacterPairingDto pairingDto in tempStory.StoryCharacterPairings)
        {
            StoryCharacterPairing pairing = new()
            {
                PairingType = pairingDto.PairingType,
                Priority    = pairingDto.Priority
            };
            foreach (int charTagId in pairingDto.MemberCharacterTagIds)
            {
                StoryCharacter? sc = actualStory.StoryCharacters
                    .FirstOrDefault(c => c.CharacterTagId == charTagId);
                if (sc is not null)
                    pairing.Members.Add(new StoryCharacterPairingMember { StoryCharacter = sc });
            }
            actualStory.StoryCharacterPairings.Add(pairing);
        }

        // ── SettingDetails ─────────────────────────────────────────────────────────
        actualStory.SettingDetails.Clear();
        foreach (SettingDetailDto detailDto in tempStory.SettingDetails)
        {
            actualStory.SettingDetails.Add(new SettingDetail
            {
                BaseTagId   = detailDto.BaseTagId,
                Name        = detailDto.Name,
                Description = detailDto.Description
            });
        }

        return actualStory;
    }
}