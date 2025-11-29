using TheCanalaveLibrary.Core.Models;
using TheCanalaveLibrary.Core.Tags;

namespace TheCanalaveLibrary.Core.Story;

public static class StoryValidations
{
    public static List<string> CanSave(this IEditableStoryProperties story)
    {
        
        List<string> errorReasons = new List<string>();

        //1. Checking for empty fields
        if (string.IsNullOrWhiteSpace(story.Title))
        {
            errorReasons.Add("Title is required");
        }

        if (string.IsNullOrWhiteSpace(story.ShortDescription))
        {
            errorReasons.Add("Short description is required");
        }
        
        if (string.IsNullOrWhiteSpace(story.LongDescription))
        {
            errorReasons.Add("Long description is required");
        }
        
        //2. Tag validation
        if (!story.StoryTags.Any(t => t.TagTypeEnum == TagTypeEnum.Setting))
        {
            errorReasons.Add("Your story must have at least one Setting tag selected.");
        }
        if (!story.StoryTags.Any(t => t.TagTypeEnum == TagTypeEnum.Genre))
        {
            errorReasons.Add("Your story must have at least one Genre tag selected.");
        }

        if (story.StoryTags.Count(t => t.TagTypeEnum == TagTypeEnum.Character && t.Priority == TagPriority.Primary) > 5)
        {
            errorReasons.Add("Your story cannot have more than 5 Primary Character tags");
        }
        if (story.StoryTags.Count(t => t.TagTypeEnum == TagTypeEnum.Genre && t.Priority == TagPriority.Primary) > 2)
        {
            errorReasons.Add("Your story cannot have more than 2 Primary Genre tags");
        }

        return errorReasons;
    }
    
    public static (bool, List<string>) CanSubmitForApproval(this IEditableStoryProperties story)
    {
        bool answer = true;
        List<string> errorReasons = new List<string>();
        //1. Post approval status must be defined by the user
        if (story.PostApprovalStatus == StoryStatusEnum.InProgress ||
            story.PostApprovalStatus == StoryStatusEnum.Completed ||
            story.PostApprovalStatus == StoryStatusEnum.OpenBeta)
        {
            
        }
        else
        {
            errorReasons.Add("You must select a Status for the story to move to once approved.");
            answer = false;
        }
        
        return (answer, errorReasons);
    }
}