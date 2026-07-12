using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="NotificationPresenter"/> (WU33).
/// Verifies: every known <see cref="NotificationTypeEnum"/> value produces non-empty Text,
/// IconPath, and AccentColor; actor-fallback to "Someone" when <c>SourceUserName</c> is null;
/// per-type icon overrides (HiddenGem → gem icon); message copy for key types.
/// </summary>
public class NotificationPresenterTests
{
    // ── Helper: construct a minimal NotificationDto ───────────────────────────────

    private static NotificationDto Make(
        NotificationTypeEnum type,
        NotificationCategoryEnum category,
        string? sourceUserName = null,
        string? targetTitle = null,
        string? targetUrl = null)
    {
        return new NotificationDto(
            NotificationId:    1L,
            NotificationTypeId: type,
            CategoryId:        category,
            SourceUserId:      sourceUserName is not null ? 42 : null,
            SourceUserName:    sourceUserName,
            TargetTitle:       targetTitle,
            TargetUrl:         targetUrl,
            RelatedEntityId:   99,
            IsRead:            false,
            DateCreated:       DateTime.UtcNow,
            Collapsed:         false);
    }

    // ── All known types produce non-empty fields ──────────────────────────────────

    [Theory]
    [MemberData(nameof(AllTypeData))]
    public void Compose_AllTypes_ReturnsNonEmptyText(NotificationTypeEnum type)
    {
        // Use YourFollows as a safe default category; individual category tests below are more precise.
        var n = Make(type, CategoryFor(type));
        NotificationPresenter.Compose(n).Text
            .Should().NotBeNullOrWhiteSpace($"{type} must produce non-empty Text");
    }

    [Theory]
    [MemberData(nameof(AllTypeData))]
    public void Compose_AllTypes_ReturnsNonEmptyIconPath(NotificationTypeEnum type)
    {
        var n = Make(type, CategoryFor(type));
        NotificationPresenter.Compose(n).IconPath
            .Should().NotBeNullOrWhiteSpace($"{type} must produce non-empty IconPath");
    }

    [Theory]
    [MemberData(nameof(AllTypeData))]
    public void Compose_AllTypes_ReturnsNonEmptyAccentColor(NotificationTypeEnum type)
    {
        var n = Make(type, CategoryFor(type));
        NotificationPresenter.Compose(n).AccentColor
            .Should().NotBeNullOrWhiteSpace($"{type} must produce non-empty AccentColor");
    }

    // ── Actor fallback ────────────────────────────────────────────────────────────

    [Fact]
    public void Compose_NewFollowerOnYou_IncludesActorName_WhenPresent()
    {
        var n = Make(NotificationTypeEnum.NewFollowerOnYou, NotificationCategoryEnum.YourFollows,
            sourceUserName: "Alice");
        var (text, _, _) = NotificationPresenter.Compose(n);
        text.Should().Contain("Alice", "actor name must appear in the message");
    }

    [Fact]
    public void Compose_NewFollowerOnYou_FallsBackToSomeone_WhenSourceUserNameNull()
    {
        var n = Make(NotificationTypeEnum.NewFollowerOnYou, NotificationCategoryEnum.YourFollows,
            sourceUserName: null);
        var (text, _, _) = NotificationPresenter.Compose(n);
        text.Should().Contain("Someone",
            "null SourceUserName must fall back to 'Someone' in the message text");
    }

    [Fact]
    public void Compose_NewVouchOnYou_FallsBackToSomeone_WhenSourceUserNameNull()
    {
        var n = Make(NotificationTypeEnum.NewVouchOnYou, NotificationCategoryEnum.YourFollows,
            sourceUserName: null);
        var (text, _, _) = NotificationPresenter.Compose(n);
        text.Should().Contain("Someone");
    }

    // ── Target entity name embedded in text ──────────────────────────────────────

    [Fact]
    public void Compose_NewStoryFavorite_IncludesTargetTitle_WhenPresent()
    {
        var n = Make(NotificationTypeEnum.NewStoryFavorite, NotificationCategoryEnum.YourStories,
            sourceUserName: "Bob", targetTitle: "The Dragon's Path");
        var (text, _, _) = NotificationPresenter.Compose(n);
        text.Should().Contain("The Dragon's Path",
            "TargetTitle must be embedded in the message for story-targeted notifications");
    }

    [Fact]
    public void Compose_NewGroupStory_IncludesGroupName_WhenPresent()
    {
        var n = Make(NotificationTypeEnum.NewGroupStory, NotificationCategoryEnum.Groups,
            targetTitle: "Legendary Authors");
        var (text, _, _) = NotificationPresenter.Compose(n);
        text.Should().Contain("Legendary Authors",
            "Group name (TargetTitle) must appear in the NewGroupStory message");
    }

    // ── Per-type icon overrides ───────────────────────────────────────────────────

    [Fact]
    public void Compose_HiddenGem_UsesGemIconPath()
    {
        var n = Make(NotificationTypeEnum.HiddenGem, NotificationCategoryEnum.YourStories,
            sourceUserName: "Carol");
        var (_, iconPath, accentColor) = NotificationPresenter.Compose(n);
        iconPath.Should().Be(RecommendationIcons.HiddenGemIconPath,
            "HiddenGem must use the gem icon (RecommendationIcons.HiddenGemIconPath) not the category default");
        accentColor.Should().Be(RecommendationIcons.HiddenGemAccentColor,
            "HiddenGem must use Torterra Emerald (RecommendationIcons.HiddenGemAccentColor)");
    }

    [Fact]
    public void Compose_NewFollowerOnYou_UsesCategoryIcon()
    {
        // Standard type with no per-type override — must fall back to category visuals.
        var n = Make(NotificationTypeEnum.NewFollowerOnYou, NotificationCategoryEnum.YourFollows);
        var (_, iconPath, accentColor) = NotificationPresenter.Compose(n);
        var cat = NotificationCategoryVisuals.For(NotificationCategoryEnum.YourFollows);
        iconPath.Should().Be(cat.IconPath,
            "types without a per-type override must use the category's icon path");
        accentColor.Should().Be(cat.AccentColor,
            "types without a per-type override must use the category's accent color");
    }

    // ── No-entity types produce safe text ────────────────────────────────────────

    [Fact]
    public void Compose_SiteAnnouncement_DoesNotContainNullLiteral()
    {
        // SiteAnnouncement has no actor and no target — verify "null" doesn't appear in text.
        var n = Make(NotificationTypeEnum.SiteAnnouncement, NotificationCategoryEnum.SiteNews,
            sourceUserName: null, targetTitle: null);
        var (text, _, _) = NotificationPresenter.Compose(n);
        text.Should().NotContain("null", "null fields must be handled gracefully, not interpolated as 'null'");
        text.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Compose_AccountWarning_DoesNotContainNullLiteral()
    {
        var n = Make(NotificationTypeEnum.AccountWarning, NotificationCategoryEnum.Warnings,
            sourceUserName: null, targetTitle: null);
        var (text, _, _) = NotificationPresenter.Compose(n);
        text.Should().NotContain("null");
    }

    // ── Mutation sanity: a missing switch arm returns non-empty (default arm) ─────

    [Fact]
    public void Compose_UnknownType_ReturnsNonEmptyDefaultText()
    {
        // Simulate a future NotificationTypeEnum value not yet in the switch.
        // Cast an arbitrary unused int — should hit the default arm, not throw.
        // Using (NotificationTypeEnum)999 as a forward-compat sentinel.
        var n = Make((NotificationTypeEnum)999, NotificationCategoryEnum.SiteNews);
        var act = () => NotificationPresenter.Compose(n);
        // Should not throw; should return non-empty text from the default arm.
        act.Should().NotThrow("the default switch arm must catch unknown future types");
        NotificationPresenter.Compose(n).Text.Should().NotBeNullOrWhiteSpace();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    /// <summary>Returns the most semantically correct category for each notification type,
    /// used to satisfy NotificationCategoryVisuals.For() without key-not-found exceptions.</summary>
    private static NotificationCategoryEnum CategoryFor(NotificationTypeEnum type) => type switch
    {
        NotificationTypeEnum.SiteAnnouncement               => NotificationCategoryEnum.SiteNews,
        NotificationTypeEnum.NewFollowerOnYou               => NotificationCategoryEnum.YourFollows,
        NotificationTypeEnum.NewVouchOnYou                  => NotificationCategoryEnum.YourFollows,
        NotificationTypeEnum.NewChapterOnFollowedStory      => NotificationCategoryEnum.YourFollows,
        NotificationTypeEnum.NewStoryByFollowedUser         => NotificationCategoryEnum.YourFollows,
        NotificationTypeEnum.NewRecommendationByFollowedUser => NotificationCategoryEnum.YourFollows,
        NotificationTypeEnum.NewBlogPostByFollowedUser      => NotificationCategoryEnum.YourFollows,
        NotificationTypeEnum.NewBlogPostOnFollowedStory     => NotificationCategoryEnum.YourFollows,
        NotificationTypeEnum.NewBlogPostOnFavoritedStory    => NotificationCategoryEnum.YourFollows,
        NotificationTypeEnum.NewBlogPostOnReadItLaterStory  => NotificationCategoryEnum.YourFollows,
        NotificationTypeEnum.NewStoryFavorite               => NotificationCategoryEnum.YourStories,
        NotificationTypeEnum.NewStoryFollower               => NotificationCategoryEnum.YourStories,
        NotificationTypeEnum.NewRecommendationOnYourStory   => NotificationCategoryEnum.YourStories,
        NotificationTypeEnum.HiddenGem                      => NotificationCategoryEnum.YourStories,
        NotificationTypeEnum.NewStoryComment                => NotificationCategoryEnum.YourStories,
        NotificationTypeEnum.YourStoryAddedToGroup          => NotificationCategoryEnum.YourStories,
        NotificationTypeEnum.TagUpdateSuggestion            => NotificationCategoryEnum.YourStories,
        NotificationTypeEnum.StoryRejected                  => NotificationCategoryEnum.YourStories,
        NotificationTypeEnum.NewStoryAcknowledgement        => NotificationCategoryEnum.YourStories,
        NotificationTypeEnum.NewCommentOnYourProfile        => NotificationCategoryEnum.YourProfile,
        NotificationTypeEnum.CommentReply                   => NotificationCategoryEnum.YourProfile,
        NotificationTypeEnum.RecommendationApproved         => NotificationCategoryEnum.YourRecommendations,
        NotificationTypeEnum.RecommendationHighlighted      => NotificationCategoryEnum.YourRecommendations,
        NotificationTypeEnum.SuccessfulRec                  => NotificationCategoryEnum.YourRecommendations,
        NotificationTypeEnum.StoryLineageRequested           => NotificationCategoryEnum.Collaborations,
        NotificationTypeEnum.StoryLineageApproved            => NotificationCategoryEnum.Collaborations,
        NotificationTypeEnum.NewGroupStory                  => NotificationCategoryEnum.Groups,
        NotificationTypeEnum.NewGroupBlogPost               => NotificationCategoryEnum.Groups,
        NotificationTypeEnum.ContentRemoved                 => NotificationCategoryEnum.Warnings,
        NotificationTypeEnum.AccountWarning                 => NotificationCategoryEnum.Warnings,
        NotificationTypeEnum.AccountSuspended               => NotificationCategoryEnum.Warnings,
        NotificationTypeEnum.AccountBanned                  => NotificationCategoryEnum.Warnings,
        NotificationTypeEnum.ReportReceived                 => NotificationCategoryEnum.YourReports,
        NotificationTypeEnum.ReportResolved                 => NotificationCategoryEnum.YourReports,
        NotificationTypeEnum.ReportResolvedNoAction         => NotificationCategoryEnum.YourReports,
        _                                                   => NotificationCategoryEnum.SiteNews
    };

    public static IEnumerable<object[]> AllTypeData() =>
        Enum.GetValues<NotificationTypeEnum>().Select(t => new object[] { t });
}
