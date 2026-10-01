using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Core;

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

        if (story.StoryCharacters.Count(sc => sc.Priority == TagPriority.Primary) > 5)
        {
            errorReasons.Add("Your story cannot have more than 5 Primary Character tags");
        }
        if (story.StoryTags.Count(t => t.TagTypeEnum == TagTypeEnum.Genre && t.Priority == TagPriority.Primary) > 2)
        {
            errorReasons.Add("Your story cannot have more than 2 Primary Genre tags");
        }

        //3. Enum binding (D1 defect (d)): an out-of-range short binds silently into either enum.
        // Any DEFINED PostApprovalStatus is accepted here — entry-set membership is checked only at
        // submit and at moderator approve, so a legacy published story whose value is e.g. OnHiatus
        // still saves (layer2-services.md §"Story Lifecycle").
        if (!Enum.IsDefined(story.Rating))
        {
            errorReasons.Add("Choose a valid rating.");
        }
        if (!Enum.IsDefined(story.PostApprovalStatus))
        {
            errorReasons.Add("Choose a valid status for when the story is published.");
        }

        return errorReasons;
    }

    public static (bool, List<string>) CanSubmitForApproval(this IEditableStoryProperties story)
    {
        // Entry-set membership (InProgress / Completed / OpenBeta) — the same predicate the server's
        // transition table and moderator approve use (StoryLifecycle; WU-StoryLifecycle, D1).
        if (StoryLifecycle.IsEntryStatus(story.PostApprovalStatus))
            return (true, []);

        return (false, [StoryLifecycle.PostApprovalStatusRequiredMessage]);
    }
}