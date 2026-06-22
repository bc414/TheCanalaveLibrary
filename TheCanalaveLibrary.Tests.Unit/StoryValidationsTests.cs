using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

public class StoryValidationsTests
{
    private static CreateStoryDTO MakeValidDto() => new()
    {
        Title = "Valid Title",
        ShortDescription = "Valid short description",
        LongDescription = "Valid long description",
        StoryTags =
        [
            new StoryTagDTO { TagId = 1, TagTypeEnum = TagTypeEnum.Setting, Priority = TagPriority.Primary },
            new StoryTagDTO { TagId = 2, TagTypeEnum = TagTypeEnum.Genre, Priority = TagPriority.Primary }
        ]
    };

    [Fact]
    public void CanSave_WithAllRequiredFieldsAndTags_ReturnsNoErrors()
    {
        MakeValidDto().CanSave().Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "Title is required")]
    public void CanSave_MissingTitle_ReturnsError(string title, string expectedError)
    {
        CreateStoryDTO dto = MakeValidDto();
        dto.Title = title;

        dto.CanSave().Should().Contain(expectedError);
    }

    [Fact]
    public void CanSave_MissingShortDescription_ReturnsError()
    {
        CreateStoryDTO dto = MakeValidDto();
        dto.ShortDescription = "";

        dto.CanSave().Should().Contain("Short description is required");
    }

    [Fact]
    public void CanSave_MissingLongDescription_ReturnsError()
    {
        CreateStoryDTO dto = MakeValidDto();
        dto.LongDescription = "";

        dto.CanSave().Should().Contain("Long description is required");
    }

    [Fact]
    public void CanSave_WithNoSettingTag_ReturnsError()
    {
        CreateStoryDTO dto = MakeValidDto();
        dto.StoryTags = [new StoryTagDTO { TagId = 2, TagTypeEnum = TagTypeEnum.Genre, Priority = TagPriority.Primary }];

        dto.CanSave().Should().Contain("Your story must have at least one Setting tag selected.");
    }

    [Fact]
    public void CanSave_WithNoGenreTag_ReturnsError()
    {
        CreateStoryDTO dto = MakeValidDto();
        dto.StoryTags = [new StoryTagDTO { TagId = 1, TagTypeEnum = TagTypeEnum.Setting, Priority = TagPriority.Primary }];

        dto.CanSave().Should().Contain("Your story must have at least one Genre tag selected.");
    }

    [Fact]
    public void CanSave_WithMoreThanFivePrimaryCharacterTags_ReturnsError()
    {
        CreateStoryDTO dto = MakeValidDto();
        for (int i = 0; i < 6; i++)
        {
            dto.StoryTags.Add(new StoryTagDTO { TagId = 100 + i, TagTypeEnum = TagTypeEnum.Character, Priority = TagPriority.Primary });
        }

        dto.CanSave().Should().Contain("Your story cannot have more than 5 Primary Character tags");
    }

    [Fact]
    public void CanSave_WithMoreThanTwoPrimaryGenreTags_ReturnsError()
    {
        CreateStoryDTO dto = MakeValidDto();
        dto.StoryTags.Add(new StoryTagDTO { TagId = 200, TagTypeEnum = TagTypeEnum.Genre, Priority = TagPriority.Primary });
        dto.StoryTags.Add(new StoryTagDTO { TagId = 201, TagTypeEnum = TagTypeEnum.Genre, Priority = TagPriority.Primary });

        dto.CanSave().Should().Contain("Your story cannot have more than 2 Primary Genre tags");
    }

    [Theory]
    [InlineData(StoryStatusEnum.InProgress, true)]
    [InlineData(StoryStatusEnum.Completed, true)]
    [InlineData(StoryStatusEnum.OpenBeta, true)]
    [InlineData(StoryStatusEnum.Draft, false)]
    [InlineData(StoryStatusEnum.OnHiatus, false)]
    public void CanSubmitForApproval_RequiresAResolvedPostApprovalStatus(StoryStatusEnum postApprovalStatus, bool expectedCanSubmit)
    {
        CreateStoryDTO dto = MakeValidDto();
        dto.PostApprovalStatus = postApprovalStatus;

        (bool canSubmit, List<string> errors) = dto.CanSubmitForApproval();

        canSubmit.Should().Be(expectedCanSubmit);
        if (!expectedCanSubmit)
        {
            errors.Should().Contain("You must select a Status for the story to move to once approved.");
        }
    }
}
