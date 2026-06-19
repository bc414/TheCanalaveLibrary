namespace TheCanalaveLibrary.Core.Story;

public class StoryValidationException : Exception
{
    public List<string> ValidationErrors { get; }

    public StoryValidationException(List<string> validationErrors)
        : base("Story validation failed.")
    {
        ValidationErrors = validationErrors;
    }
}