using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

public class StoryMappersTests
{
    private static CreateStoryDTO MakeDto() => new()
    {
        Title = "A New Story",
        ShortDescription = "Short desc",
        Rating = Rating.T,
        StoryStatusId = StoryStatusEnum.InProgress,
        CoverArtRelativeUrl = "/uploads/stories/1/cover-abc.png",
        LongDescription = "Long desc",
        PostApprovalStatus = StoryStatusEnum.InProgress,
        StoryTags = [new StoryTagDTO { TagId = 5, Priority = TagPriority.Primary, TagTypeEnum = TagTypeEnum.Genre }]
    };

    [Fact]
    public void ToStory_InitializesStoryListingAndStoryDetail()
    {
        // Regression test for the WU12 NRE: a bare `new Story()` left StoryListing/StoryDetail
        // null!, and the very next line (UpdateStoryEditableProperties) dereferenced both.
        Story story = MakeDto().ToStory();

        story.StoryListing.Should().NotBeNull();
        story.StoryDetail.Should().NotBeNull();
    }

    [Fact]
    public void ToStory_MapsFieldsAcrossThePartitionTrio()
    {
        CreateStoryDTO dto = MakeDto();

        Story story = dto.ToStory();

        story.StoryListing.StoryTitle.Should().Be(dto.Title);
        story.StoryListing.ShortDescription.Should().Be(dto.ShortDescription);
        story.StoryListing.CoverArtRelativeUrl.Should().Be(dto.CoverArtRelativeUrl);
        story.StoryDetail.LongDescription.Should().Be(dto.LongDescription);
        story.StoryDetail.PostApprovalStatus.Should().Be(dto.PostApprovalStatus);
        story.Rating.Should().Be(dto.Rating);
        story.StoryStatusId.Should().Be(dto.StoryStatusId);
    }

    [Fact]
    public void ToStory_MapsStoryTags()
    {
        CreateStoryDTO dto = MakeDto();

        Story story = dto.ToStory();

        story.StoryTags.Should().ContainSingle()
            .Which.TagId.Should().Be(5);
    }

    [Fact]
    public void UpdateStoryEditableProperties_ReplacesExistingTags_RatherThanAppending()
    {
        Story story = MakeDto().ToStory(); // seeds one tag (TagId 5)

        StoryUpdateDTO update = new()
        {
            StoryId = 1,
            Title = "Updated Title",
            Rating = Rating.M,
            StoryStatusId = StoryStatusEnum.Completed,
            PostApprovalStatus = StoryStatusEnum.Completed,
            StoryTags = [new StoryTagDTO { TagId = 9, Priority = TagPriority.Supporting, TagTypeEnum = TagTypeEnum.Setting }]
        };

        story.UpdateStoryEditableProperties(update);

        story.StoryTags.Should().ContainSingle()
            .Which.TagId.Should().Be(9);
        story.StoryListing.StoryTitle.Should().Be("Updated Title");
        story.Rating.Should().Be(Rating.M);
    }
}
