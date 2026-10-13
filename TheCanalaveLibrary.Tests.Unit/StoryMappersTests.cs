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
        story.StoryDetail.LongDescription.Should().Be(dto.LongDescription);
        story.StoryDetail.PostApprovalStatus.Should().Be(dto.PostApprovalStatus);
        story.Rating.Should().Be(dto.Rating);
        story.StoryStatusId.Should().Be(default(StoryStatusEnum),
            "the mapper never copies status — a lifecycle move is not a property edit (WU-StoryLifecycle, D1)");
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
        story.StoryStatusId = StoryStatusEnum.InProgress;

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
        story.StoryStatusId.Should().Be(StoryStatusEnum.InProgress,
            "StoryUpdateDTO.StoryStatusId is a read echo — the mapper must ignore it (WU-StoryLifecycle, D1)");
    }

    /// <summary>
    /// The inverse hazard of owner ruling D14, at mapper level: the mapper once copied the cover
    /// unconditionally from the DTO, so an update that said nothing about the cover nulled the column
    /// (and UpdateStoryAsync then deleted the live blob). The cover is off every write DTO now, and a
    /// property edit must leave it exactly as it was — even when the edit arrives as a StoryEditDTO,
    /// whose cover echo is a different value.
    /// </summary>
    [Fact]
    public void UpdateStoryEditableProperties_LeavesTheStoredCoverUntouched()
    {
        Story story = MakeDto().ToStory();
        story.StoryListing.CoverArtRelativeUrl = "/uploads/stories/1/cover-11111111-1111-1111-1111-111111111111.png";

        story.UpdateStoryEditableProperties(new StoryEditDTO
        {
            StoryId = 1,
            Title = "Edited",
            CoverArtRelativeUrl = null, // a read echo the mapper must never read
        });

        story.StoryListing.StoryTitle.Should().Be("Edited");
        story.StoryListing.CoverArtRelativeUrl.Should().Be(
            "/uploads/stories/1/cover-11111111-1111-1111-1111-111111111111.png",
            "a property edit cannot set, clear or delete the cover (owner ruling D14)");
    }

    [Fact]
    public void ToStory_LeavesTheCoverUnset()
    {
        // A new story gets its cover from UploadCoverArtAsync after the create, never from the DTO.
        MakeDto().ToStory().StoryListing.CoverArtRelativeUrl.Should().BeNull();
    }
}
