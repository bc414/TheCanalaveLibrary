using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Tracker D21 (WU-D21Confidentiality, 2026-10-14): a notification never names or links a story outside
/// the public window (Draft, PendingApproval, Rejected) or taken down, unless its recipient is the
/// story's author. Rule: <c>layer2-services.md</c> §"Polymorphic RelatedEntityId" → "Story-family kinds
/// by recipient". <see cref="NotificationEnricher"/>'s <c>Story</c>, <c>Chapter</c> and
/// <c>GroupStory</c> kinds each split into an author-recipient <c>Own*</c> kind (resolved as stored, but
/// never linking a page that 404s for the author: a taken-down story, an unpublished chapter) and a
/// filtered kind for everyone else.
///
/// <para><b>Per-test seeding:</b> <see cref="InitializeAsync"/> seeds the story's author and one other
/// user (every non-author recipient). Stories come from <c>SeedStoryAsync</c>; groups, joins, adds,
/// lineage requests and credits go through the real write services, so their FK parents exist. The
/// rows of types whose producer is a stub (tracker B24), or whose producer is not the point of the
/// test, are inserted directly (the D8 tests' shape): the enricher is what is under test. Respawn
/// resets the database between tests.</para>
///
/// Tier: Integration (real Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class StoryNotificationConfidentialityTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _authorId;
    private int _readerId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _authorId = await SeedUserAsync("d21-author");
        _readerId = await SeedUserAsync("d21-reader");
    }

    public enum Hiding { None, Draft, PendingApproval, Rejected, TakenDown }

    public enum ChapterHiding { None, StoryDraft, StoryTakenDown, ChapterUnpublished }

    /// <summary>Story-kind types whose recipient is always the referenced story's author.</summary>
    private static readonly NotificationTypeEnum[] AuthorRecipientStoryTypes =
    [
        NotificationTypeEnum.NewStoryFavorite, NotificationTypeEnum.NewStoryFollower,
        NotificationTypeEnum.NewRecommendationOnYourStory, NotificationTypeEnum.RecommendationRevised,
        NotificationTypeEnum.StoryRejected, NotificationTypeEnum.StoryApproved,
        NotificationTypeEnum.ExternalLinkVerified, NotificationTypeEnum.ExternalLinkRejected,
        NotificationTypeEnum.StorySpotlighted,
    ];

    /// <summary>Story-kind types whose recipient is anyone but the referenced story's author.</summary>
    private static readonly NotificationTypeEnum[] OtherRecipientStoryTypes =
    [
        NotificationTypeEnum.NewStoryByFollowedUser, NotificationTypeEnum.NewRecommendationByFollowedUser,
        NotificationTypeEnum.RecommendationApproved, NotificationTypeEnum.RecommendationHighlighted,
        NotificationTypeEnum.SuccessfulRec, NotificationTypeEnum.RecommendationRevisionRequested,
        NotificationTypeEnum.StoryLineageRequested, NotificationTypeEnum.StoryLineageApproved,
        NotificationTypeEnum.NewStoryAcknowledgement, NotificationTypeEnum.RecommendationSpotlighted,
    ];

    // ── GroupStory kind (types 60 / 25) ──────────────────────────────────────────

    [Fact]
    public async Task NewGroupStory_AnAuthorsOwnDraftFiledIntoTheirGroup_IsNotNamedToAMember()
    {
        // The tracker's probe. As built, AddStoryAsync accepts an author's own draft and the 60 fans out
        // at add time; decision row 31 / tracker F28 is open on both. Its setup changes if the owner
        // rules "no" (the add is refused) or "yes, tell members at publish time" (no 60 at add time);
        // the unpublished-after-the-fan-out test below keeps covering the same lookup either way.
        SetActiveUser(_authorId);
        int groupId = await CreateGroupAsync("D21 Probe Group");
        int draftId = await SeedStoryAsync(authorId: _authorId, status: StoryStatusEnum.Draft);
        SetActiveUser(_readerId);
        await JoinGroupAsync(groupId);

        SetActiveUser(_authorId);
        await AddStoryAsync(groupId, draftId);

        NotificationDto toMember = await SingleAsync(_readerId, NotificationTypeEnum.NewGroupStory);
        toMember.TargetTitle.Should().Be("D21 Probe Group", "the member can still open the group");
        toMember.TargetUrl.Should().Be($"/group/{groupId}");
        toMember.TargetContextTitle.Should().BeNull("a draft is its author's alone, and the member was told its title");
    }

    [Theory]
    [InlineData(Hiding.None)]
    [InlineData(Hiding.Draft)]
    [InlineData(Hiding.TakenDown)]
    public async Task GroupStoryTypes_AStoryHiddenAfterTheFanOut_LosesItsTitleForMembers_NotForItsAuthor(Hiding hiding)
    {
        // Minted while the story was public: the leak is the lookup at read time, not the mint.
        int creatorId = await SeedUserAsync("d21-creator");
        SetActiveUser(creatorId);
        int groupId = await CreateGroupAsync("D21 Hidden Later");
        int storyId = await SeedStoryAsync(authorId: _authorId);
        string title = await TitleOfAsync(storyId);
        SetActiveUser(_readerId);
        await JoinGroupAsync(groupId);
        SetActiveUser(creatorId);
        await AddStoryAsync(groupId, storyId);

        await HideAsync(storyId, hiding);

        NotificationDto toMember = await SingleAsync(_readerId, NotificationTypeEnum.NewGroupStory);
        toMember.TargetTitle.Should().Be("D21 Hidden Later");
        toMember.TargetUrl.Should().Be($"/group/{groupId}");
        toMember.TargetContextTitle.Should().Be(hiding == Hiding.None ? title : null);

        NotificationDto toAuthor = await SingleAsync(_authorId, NotificationTypeEnum.YourStoryAddedToGroup);
        toAuthor.TargetTitle.Should().Be("D21 Hidden Later");
        toAuthor.TargetUrl.Should().Be($"/group/{groupId}");
        toAuthor.TargetContextTitle.Should().Be(title, "the story's author sees their own story in any state");
    }

    [Fact]
    public async Task NewGroupStoryEmail_ForAnAuthorsOwnDraft_CarriesTheGroupButNotTheStoryTitle()
    {
        // The email composer shares the enricher, so the mail drops the title the bell drops. Same
        // setup as the probe above, so the same dependency: an author's own draft is accepted and the
        // 60 fires at add time, both open under decision row 31 / tracker F28. A "no" or a "yes, at
        // publish time" moves this test onto a story hidden after the fan-out, before the flush.
        SetActiveUser(_authorId);
        int groupId = await CreateGroupAsync("D21 Mail Group");
        int draftId = await SeedStoryAsync(authorId: _authorId, status: StoryStatusEnum.Draft);
        string title = await TitleOfAsync(draftId);
        SetActiveUser(_readerId);
        await JoinGroupAsync(groupId);
        await EnableEmailAsync(_readerId, NotificationTypeEnum.NewGroupStory); // the type defaults to off

        SetActiveUser(_authorId);
        await AddStoryAsync(groupId, draftId);
        using (IServiceScope scope = Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<OutboundEmailFlusher>().FlushAsync();

        OutgoingMail mail = Factory.Services.GetRequiredService<RecordingMailTransport>().Sent
            .Should().ContainSingle(m => m.UserId == _readerId
                                         && m.Kind == $"Notification.{NotificationTypeEnum.NewGroupStory}")
            .Subject;
        mail.HtmlBody.Should().Contain("D21 Mail Group").And.NotContain(title);
        mail.Subject.Should().NotContain(title);
    }

    // ── Story kind (every type that links /story/{id}) ───────────────────────────

    [Theory]
    [InlineData(Hiding.None)]
    [InlineData(Hiding.Draft)]
    [InlineData(Hiding.PendingApproval)]
    [InlineData(Hiding.Rejected)]
    [InlineData(Hiding.TakenDown)]
    public async Task StoryKind_AHiddenStory_IsNamedAndLinkedOnlyToItsAuthor(Hiding hiding)
    {
        int storyId = await SeedStoryAsync(authorId: _authorId, rating: Rating.M);
        string title = await TitleOfAsync(storyId);
        await HideAsync(storyId, hiding);
        await SeedNotificationsAsync(_authorId, storyId, AuthorRecipientStoryTypes);
        await SeedNotificationsAsync(_readerId, storyId, OtherRecipientStoryTypes);

        // Both recipients have mature content off: the M rating never hides anything (Personal plane).
        NotificationDto[] toAuthor = await NotificationsOfAsync(_authorId);
        toAuthor.Should().HaveCount(AuthorRecipientStoryTypes.Length);
        foreach (NotificationDto dto in toAuthor)
        {
            dto.TargetTitle.Should().Be(title, $"{dto.NotificationTypeId}'s recipient is the story's author");
            // The story page keeps the removal filter for its author too, so a takedown drops only the link.
            dto.TargetUrl.Should().Be(hiding == Hiding.TakenDown ? null : $"/story/{storyId}",
                "a taken-down story's page is a 404 for its author as well");
        }

        NotificationDto[] toReader = await NotificationsOfAsync(_readerId);
        toReader.Should().HaveCount(OtherRecipientStoryTypes.Length);
        foreach (NotificationDto dto in toReader)
        {
            dto.TargetTitle.Should().Be(hiding == Hiding.None ? title : null,
                $"{dto.NotificationTypeId}'s recipient is not the story's author");
            dto.TargetUrl.Should().Be(hiding == Hiding.None ? $"/story/{storyId}" : null);
        }
    }

    [Fact]
    public async Task EveryTypeThatLinksAStory_IsClassifiedByWhomItTells()
    {
        // A type that resolves to /story/{id} must sit in exactly one of the two lists above, so a type
        // newly mapped to a story kind cannot ship without someone deciding whether its recipient is the
        // author (layer2-services.md: "Classifying a new type").
        int storyId = await SeedStoryAsync(authorId: _authorId);
        NotificationTypeEnum[] all = Enum.GetValues<NotificationTypeEnum>();
        await SeedNotificationsAsync(_readerId, storyId, all);

        NotificationDto[] dtos = await NotificationsOfAsync(_readerId);
        dtos.Should().HaveCount(all.Length);
        dtos.Where(d => d.TargetUrl == $"/story/{storyId}").Select(d => d.NotificationTypeId)
            .Should().BeEquivalentTo(AuthorRecipientStoryTypes.Concat(OtherRecipientStoryTypes));
    }

    [Fact]
    public async Task StoryLineageRequested_FromADraftSource_DoesNotNameTheDraftToTheTargetsAuthor()
    {
        // The source is only ownership-gated, so a draft can be the requester's source story. Whether
        // such a request may exist at all is decision row 32 / tracker F29; if the owner refuses it,
        // this setup changes (the request is rejected) and the Story-kind theory keeps covering 50.
        int draftId = await SeedStoryAsync(authorId: _authorId, status: StoryStatusEnum.Draft);
        int targetId = await SeedStoryAsync(authorId: _readerId);
        SetActiveUser(_authorId);
        using (IServiceScope scope = Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IStoryLineageWriteService>().RequestLineageAsync(
                new CreateStoryLineageDto { SourceStoryId = draftId, TargetStoryId = targetId, TypeId = 1 });

        NotificationDto dto = await SingleAsync(_readerId, NotificationTypeEnum.StoryLineageRequested);
        dto.RelatedEntityId.Should().Be(draftId);
        dto.TargetTitle.Should().BeNull();
        dto.TargetUrl.Should().BeNull();
    }

    [Fact]
    public async Task NewStoryAcknowledgement_OnADraft_NamesTheStoryOnlyOnceItIsPublished()
    {
        // As built, a credit on a draft is accepted. Whether it may be is decision row 32 / tracker
        // F29; if the owner refuses it, this setup changes and the Story-kind theory keeps covering 52.
        int draftId = await SeedStoryAsync(authorId: _authorId, status: StoryStatusEnum.Draft);
        string title = await TitleOfAsync(draftId);
        SetActiveUser(_authorId);
        using (IServiceScope scope = Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IStoryAcknowledgmentWriteService>().RequestAcknowledgmentAsync(
                new CreateStoryAcknowledgmentDto { StoryId = draftId, AcknowledgedUserId = _readerId, AcknowledgmentRoleId = 1 });

        NotificationDto beforePublish = await SingleAsync(_readerId, NotificationTypeEnum.NewStoryAcknowledgement);
        beforePublish.TargetTitle.Should().BeNull("the credited user cannot open a draft");
        beforePublish.TargetUrl.Should().BeNull();

        // The window is applied when the notification is read, so publishing names it.
        await SetStatusAsync(draftId, StoryStatusEnum.InProgress);
        NotificationDto afterPublish = await SingleAsync(_readerId, NotificationTypeEnum.NewStoryAcknowledgement);
        afterPublish.TargetTitle.Should().Be(title);
        afterPublish.TargetUrl.Should().Be($"/story/{draftId}");
    }

    // ── Chapter kind (types 10 / 24) ─────────────────────────────────────────────

    [Theory]
    [InlineData(ChapterHiding.None)]
    [InlineData(ChapterHiding.StoryDraft)]
    [InlineData(ChapterHiding.StoryTakenDown)]
    [InlineData(ChapterHiding.ChapterUnpublished)]
    public async Task ChapterKind_AHiddenChapterOrStory_IsNamedOnlyToTheStorysAuthor(ChapterHiding hiding)
    {
        int storyId = await SeedStoryAsync(authorId: _authorId);
        string storyTitle = await TitleOfAsync(storyId);
        int chapterId = await SeedChapterAsync(storyId, "D21 Chapter");
        switch (hiding)
        {
            case ChapterHiding.StoryDraft: await HideAsync(storyId, Hiding.Draft); break;
            case ChapterHiding.StoryTakenDown: await HideAsync(storyId, Hiding.TakenDown); break;
            case ChapterHiding.ChapterUnpublished: await UnpublishChapterAsync(chapterId); break;
        }
        await SeedNotificationsAsync(_readerId, chapterId, [NotificationTypeEnum.NewChapterOnFollowedStory]);
        await SeedNotificationsAsync(_authorId, chapterId, [NotificationTypeEnum.NewStoryComment]);

        bool visible = hiding == ChapterHiding.None;
        NotificationDto toFollower = await SingleAsync(_readerId, NotificationTypeEnum.NewChapterOnFollowedStory);
        toFollower.TargetTitle.Should().Be(visible ? "D21 Chapter" : null);
        toFollower.TargetUrl.Should().Be(visible ? $"/story/{storyId}/1" : null);
        toFollower.TargetContextTitle.Should().Be(visible ? storyTitle : null);

        NotificationDto toAuthor = await SingleAsync(_authorId, NotificationTypeEnum.NewStoryComment);
        toAuthor.TargetTitle.Should().Be("D21 Chapter", "24's recipient is the story's author");
        toAuthor.TargetContextTitle.Should().Be(storyTitle);
        // The reading page serves a published chapter of a story that is not taken down, to its
        // author too, so those two hidings drop only the link.
        bool readable = hiding is ChapterHiding.None or ChapterHiding.StoryDraft;
        toAuthor.TargetUrl.Should().Be(readable ? $"/story/{storyId}/1" : null,
            "the author's link must not lead to a page that 404s for them");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<int> CreateGroupAsync(string name)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IGroupWriteService>()
            .CreateGroupAsync(new CreateGroupDto { GroupName = name });
    }

    private async Task JoinGroupAsync(int groupId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IGroupWriteService>().JoinAsync(groupId);
    }

    private async Task AddStoryAsync(int groupId, int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IGroupWriteService>()
            .AddStoryAsync(new AddGroupStoryDto { GroupId = groupId, StoryId = storyId });
    }

    private async Task<string> TitleOfAsync(int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().StoryListings
            .Where(l => l.StoryId == storyId).Select(l => l.StoryTitle).SingleAsync();
    }

    private async Task HideAsync(int storyId, Hiding hiding)
    {
        switch (hiding)
        {
            case Hiding.None: return;
            case Hiding.TakenDown:
            {
                using IServiceScope scope = Factory.Services.CreateScope();
                ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Story story = await db.Stories.SingleAsync(s => s.StoryId == storyId);
                story.IsTakenDown = true;
                story.TakedownDate = DateTime.UtcNow;
                await db.SaveChangesAsync();
                return;
            }
            default:
                await SetStatusAsync(storyId, hiding switch
                {
                    Hiding.Draft => StoryStatusEnum.Draft,
                    Hiding.PendingApproval => StoryStatusEnum.PendingApproval,
                    _ => StoryStatusEnum.Rejected,
                });
                return;
        }
    }

    /// <summary>Sets the status directly; a published status gets its publish stamp (ck_stories_published_date).</summary>
    private async Task SetStatusAsync(int storyId, StoryStatusEnum status)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Story story = await db.Stories.SingleAsync(s => s.StoryId == storyId);
        story.StoryStatusId = status;
        if (StoryLifecycle.IsPublished(status)) story.PublishedDate ??= DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private async Task<int> SeedChapterAsync(int storyId, string title)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Chapter chapter = new()
        {
            StoryId = storyId, ChapterNumber = 1, Title = title,
            PrimaryContentId = null, IsPublished = true, FirstPublishedDate = DateTime.UtcNow, VersionCount = 0
        };
        db.Chapters.Add(chapter);
        await db.SaveChangesAsync();
        return chapter.ChapterId;
    }

    private async Task UnpublishChapterAsync(int chapterId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Chapters
            .Where(c => c.ChapterId == chapterId)
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.IsPublished, false));
    }

    private async Task SeedNotificationsAsync(int recipientId, long relatedEntityId, IEnumerable<NotificationTypeEnum> types)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        foreach (NotificationTypeEnum type in types)
        {
            db.Notifications.Add(new Notification
            {
                RecipientUserId = recipientId, NotificationTypeId = type, SourceUserId = null,
                RelatedEntityId = relatedEntityId, DateCreated = DateTime.UtcNow,
            });
        }
        await db.SaveChangesAsync();
    }

    private async Task EnableEmailAsync(int userId, NotificationTypeEnum type)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.UserNotificationSettings.Add(new UserNotificationSetting
        {
            UserId = userId, NotificationTypeId = type, EmailEnabled = true, Collapsed = null
        });
        await db.SaveChangesAsync();
    }

    private async Task<NotificationDto[]> NotificationsOfAsync(int recipientId)
    {
        SetActiveUser(recipientId);
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<INotificationReadService>()
            .GetNotificationsAsync(1, 100);
    }

    private async Task<NotificationDto> SingleAsync(int recipientId, NotificationTypeEnum type) =>
        (await NotificationsOfAsync(recipientId)).Single(n => n.NotificationTypeId == type);
}
