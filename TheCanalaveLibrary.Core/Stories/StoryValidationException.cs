namespace TheCanalaveLibrary.Core;

public class StoryValidationException(List<string> validationErrors)
    : CanalaveValidationException("Story validation failed.", validationErrors);