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
            // The source is already a List<IStoryTag>. We create a new list
            // to avoid sharing the same list instance (defensive copy).
            StoryTags = new List<IStoryTag>(story.StoryTags)
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
            StoryTags = new List<IStoryTag>(story.StoryTags)
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

        // This is the correct way to update a navigation collection.
        // Replacing the collection directly can cause EF Core tracking issues.
        actualStory.StoryTags.Clear();
        foreach (IStoryTag tempTag in tempStory.StoryTags)
        {
            actualStory.StoryTags.Add(tempTag.ToStoryTag());
        }

        return actualStory;
    }
}