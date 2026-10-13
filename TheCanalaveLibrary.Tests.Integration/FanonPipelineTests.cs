using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for the fanonization pipeline (WU-TagFanon Groups 6–8): dashboard grouping +
/// threshold + normalization, the moderator link-and-notify act (never the same author twice per
/// tag), author adoption (in-place, nuance/priority/pairings survive, collisions skip), and
/// reversible dismissal. Tier: Integration (Testcontainers Postgres).
/// </summary>
[Collection("Postgres")]
public class FanonPipelineTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _authorA;
    private int _authorB;
    private int _modId;
    private int _baseTagId;      // OC base species (AllowCustomName)
    private int _targetTagId;    // the official tag mods link to

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _authorA = await SeedUserAsync("FanonAuthorA");
        _authorB = await SeedUserAsync("FanonAuthorB");
        _modId = await SeedUserAsync("FanonMod");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string s = Guid.NewGuid().ToString("N")[..8];
        Tag baseTag = new() { TagName = $"Species-{s}", TagTypeId = TagTypeEnum.Character, AllowCustomName = true };
        Tag target = new() { TagName = $"Saura-{s} (Saga)", TagTypeId = TagTypeEnum.Character, IsFanon = true, ParentTag = baseTag };
        db.Tags.AddRange(baseTag, target);
        await db.SaveChangesAsync();
        _baseTagId = baseTag.TagId;
        _targetTagId = target.TagId;
    }

    /// <summary>Seeds one story per author carrying the OC name (casing per caller) on the base tag.</summary>
    private async Task<int> SeedClusterStoryAsync(int authorId, string ocName, string? nuance = null,
        StoryStatusEnum status = StoryStatusEnum.InProgress)
    {
        int storyId = await SeedStoryAsync(authorId, status: status);
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.StoryCharacters.Add(new StoryCharacter
        {
            StoryId = storyId, CharacterTagId = _baseTagId, Priority = TagPriority.Supporting,
            IsOc = true, CustomName = ocName, Nuance = nuance
        });
        await db.SaveChangesAsync();
        return storyId;
    }

    private IServiceScope NewScope() => Factory.Services.CreateScope();

    // ── Dashboard grouping ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetGroupsAsync_GroupsCaseInsensitively_AcrossAuthors()
    {
        string name = $"Grp{Guid.NewGuid():N}"[..12];
        await SeedClusterStoryAsync(_authorA, name);
        await SeedClusterStoryAsync(_authorB, name.ToLowerInvariant());

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorA, showMatureContent: false));
        using IServiceScope scope = NewScope();
        IFanonReadService fanon = scope.ServiceProvider.GetRequiredService<IFanonReadService>();

        IReadOnlyList<FanonGroupDto> groups = await fanon.GetGroupsAsync(TagTypeEnum.Character, name, 1, 50);

        FanonGroupDto group = groups.Should().ContainSingle(g => g.BaseTag.TagId == _baseTagId).Subject;
        group.StoryCount.Should().Be(2, "both casings normalize into one group");
        group.AuthorCount.Should().Be(2);
    }

    [Fact]
    public async Task GetGroupsAsync_SingleAuthorGroup_StaysBelowThreshold()
    {
        string name = $"Solo{Guid.NewGuid():N}"[..12];
        await SeedClusterStoryAsync(_authorA, name);

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorA, showMatureContent: false));
        using IServiceScope scope = NewScope();
        IFanonReadService fanon = scope.ServiceProvider.GetRequiredService<IFanonReadService>();

        IReadOnlyList<FanonGroupDto> groups = await fanon.GetGroupsAsync(TagTypeEnum.Character, name, 1, 50);

        groups.Should().BeEmpty("one distinct author is below the default reach threshold (2) — same gate for every viewer");
    }

    [Fact]
    public async Task GetGroupsAsync_DraftStories_DoNotFeedPublicReach()
    {
        string name = $"Drft{Guid.NewGuid():N}"[..12];
        await SeedClusterStoryAsync(_authorA, name, status: StoryStatusEnum.Draft);
        await SeedClusterStoryAsync(_authorB, name, status: StoryStatusEnum.Draft);

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorA, showMatureContent: false));
        using IServiceScope scope = NewScope();
        IFanonReadService fanon = scope.ServiceProvider.GetRequiredService<IFanonReadService>();

        (await fanon.GetGroupsAsync(TagTypeEnum.Character, name, 1, 50))
            .Should().BeEmpty("drafts never leak into public reach counts");
    }

    [Fact]
    public async Task GetGroupStoriesAsync_GatesListButKeepsCompleteCount()
    {
        string name = $"Gate{Guid.NewGuid():N}"[..12];
        await SeedClusterStoryAsync(_authorA, name);
        int matureStory = await SeedStoryAsync(_authorB, rating: Rating.M);
        using (IServiceScope seedScope = NewScope())
        {
            ApplicationDbContext db = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.StoryCharacters.Add(new StoryCharacter
            {
                StoryId = matureStory, CharacterTagId = _baseTagId, IsOc = true, CustomName = name
            });
            await db.SaveChangesAsync();
        }

        // Mature-off viewer: complete count, filtered visible list (count-line disclosure shape).
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorA, showMatureContent: false));
        using IServiceScope scope = NewScope();
        IFanonReadService fanon = scope.ServiceProvider.GetRequiredService<IFanonReadService>();

        FanonGroupStoriesDto stories = await fanon.GetGroupStoriesAsync(TagTypeEnum.Character, _baseTagId, name);

        stories.TotalCount.Should().Be(2, "counts are complete for every viewer");
        stories.Visible.Should().HaveCount(1, "the M story is hidden under the viewer's consent");
    }

    // ── Link and notify ────────────────────────────────────────────────────────

    [Fact]
    public async Task LinkGroupAsync_NonMod_Throws()
    {
        SetActiveUser(_authorA);
        using IServiceScope scope = NewScope();
        IFanonWriteService fanon = scope.ServiceProvider.GetRequiredService<IFanonWriteService>();

        Func<Task> act = () => fanon.LinkGroupAsync(new FanonLinkCreateDto("X", _baseTagId, _targetTagId));
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task LinkGroupAsync_NotifiesEachAuthorOnce_AndNeverTwice()
    {
        string name = $"Link{Guid.NewGuid():N}"[..12];
        await SeedClusterStoryAsync(_authorA, name);
        await SeedClusterStoryAsync(_authorB, name);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using (IServiceScope scope = NewScope())
        {
            IFanonWriteService fanon = scope.ServiceProvider.GetRequiredService<IFanonWriteService>();
            int notified = await fanon.LinkGroupAsync(new FanonLinkCreateDto(name, _baseTagId, _targetTagId));
            notified.Should().Be(2);
        }

        using (IServiceScope scope = NewScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Notifications.CountAsync(n =>
                    n.NotificationTypeId == NotificationTypeEnum.TagUpdateSuggestion
                    && n.RelatedEntityId == _targetTagId
                    && n.SourceUserId == null))
                .Should().Be(2, "one type-26 invitation per author, RelatedEntityId = target tag, null-sourced (D5 sub-edge: the moderator is not named)");
        }

        // Author A reads theirs, then a re-sweep runs — nobody is re-notified (the never-twice
        // rule lives in TagAdoptionState.DateNotified, NOT in unread-dedup).
        using (IServiceScope scope = NewScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Notifications
                .Where(n => n.RecipientUserId == _authorA)
                .ExecuteUpdateAsync(u => u.SetProperty(n => n.IsRead, true));
        }
        using (IServiceScope scope = NewScope())
        {
            IFanonWriteService fanon = scope.ServiceProvider.GetRequiredService<IFanonWriteService>();
            (await fanon.NotifyNewAuthorsAsync(name, _baseTagId)).Should().Be(0);
        }

        // A third author arrives later — only they get notified by the next sweep.
        int authorC = await SeedUserAsync("FanonAuthorC");
        await SeedClusterStoryAsync(authorC, name);
        using (IServiceScope scope = NewScope())
        {
            IFanonWriteService fanon = scope.ServiceProvider.GetRequiredService<IFanonWriteService>();
            (await fanon.NotifyNewAuthorsAsync(name, _baseTagId)).Should().Be(1);
        }
    }

    /// <summary>
    /// Service audit §2.12 (WU-ParityAndRemaining, 2026-10-13): the invite sweep runs on the write
    /// context, which carries no query filters, so its doc's "the IsTakenDown filter stays active" was
    /// false and a moderator-removed story drew an adoption invitation. The exclusion is now an explicit
    /// <c>!IsTakenDown</c> in the author query.
    /// </summary>
    [Fact]
    public async Task LinkGroupAsync_AnAuthorWhoseOnlyMatchingStoryIsTakenDown_IsNotInvited()
    {
        string name = $"Down{Guid.NewGuid():N}"[..12];
        await SeedClusterStoryAsync(_authorA, name);
        int removedStoryId = await SeedClusterStoryAsync(_authorB, name);
        using (IServiceScope scope = NewScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Stories.Where(st => st.StoryId == removedStoryId)
                .ExecuteUpdateAsync(u => u.SetProperty(st => st.IsTakenDown, true));
        }

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using (IServiceScope scope = NewScope())
        {
            IFanonWriteService fanon = scope.ServiceProvider.GetRequiredService<IFanonWriteService>();
            (await fanon.LinkGroupAsync(new FanonLinkCreateDto(name, _baseTagId, _targetTagId)))
                .Should().Be(1, "only the live story's author is invited");
        }

        using (IServiceScope scope = NewScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Notifications.Where(n => n.NotificationTypeId == NotificationTypeEnum.TagUpdateSuggestion)
                    .Select(n => n.RecipientUserId).ToListAsync())
                .Should().Equal([_authorA]);
            (await db.TagAdoptionStates.Where(st => st.TargetTagId == _targetTagId && st.DateNotified != null)
                    .Select(st => st.UserId).ToListAsync())
                .Should().Equal([_authorA], "the removed story's author is not marked as told either");
        }
    }

    /// <summary>
    /// The flat-tag twin of the test above (WU-ParityAndRemaining review fixes, 2026-10-13): the sweep
    /// has a second author query, over <c>StoryTags</c>, for every non-Character axis, and its own
    /// <c>!IsTakenDown</c> had no test. A Setting base tag with custom names on two stories, one taken down.
    /// </summary>
    [Fact]
    public async Task LinkGroupAsync_OnAFlatTagAxis_AnAuthorWhoseOnlyMatchingStoryIsTakenDown_IsNotInvited()
    {
        string name = $"Flat{Guid.NewGuid():N}"[..12];
        int flatBaseId, flatTargetId;
        using (IServiceScope scope = NewScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            string s = Guid.NewGuid().ToString("N")[..8];
            Tag flatBase = new() { TagName = $"Region-{s}", TagTypeId = TagTypeEnum.Setting, AllowCustomName = true };
            Tag flatTarget = new() { TagName = $"Orre-{s}", TagTypeId = TagTypeEnum.Setting, IsFanon = true, ParentTag = flatBase };
            db.Tags.AddRange(flatBase, flatTarget);
            await db.SaveChangesAsync();
            flatBaseId = flatBase.TagId;
            flatTargetId = flatTarget.TagId;
        }

        int liveStoryId = await SeedStoryAsync(_authorA);
        int removedStoryId = await SeedStoryAsync(_authorB);
        using (IServiceScope scope = NewScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.StoryTags.AddRange(
                new StoryTag { StoryId = liveStoryId, TagId = flatBaseId, Priority = TagPriority.Supporting, CustomName = name },
                new StoryTag { StoryId = removedStoryId, TagId = flatBaseId, Priority = TagPriority.Supporting, CustomName = name });
            await db.SaveChangesAsync();
            await db.Stories.Where(st => st.StoryId == removedStoryId)
                .ExecuteUpdateAsync(u => u.SetProperty(st => st.IsTakenDown, true));
        }

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using (IServiceScope scope = NewScope())
        {
            IFanonWriteService fanon = scope.ServiceProvider.GetRequiredService<IFanonWriteService>();
            (await fanon.LinkGroupAsync(new FanonLinkCreateDto(name, flatBaseId, flatTargetId)))
                .Should().Be(1, "only the live story's author is invited");
        }

        using (IServiceScope scope = NewScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Notifications.Where(n => n.NotificationTypeId == NotificationTypeEnum.TagUpdateSuggestion)
                    .Select(n => n.RecipientUserId).ToListAsync())
                .Should().Equal([_authorA]);
            (await db.TagAdoptionStates.Where(st => st.TargetTagId == flatTargetId && st.DateNotified != null)
                    .Select(st => st.UserId).ToListAsync())
                .Should().Equal([_authorA], "the removed story's author is not marked as told either");
        }
    }

    [Fact]
    public async Task LinkGroupAsync_TheLinkingModeratorsOwnUse_GetsNoInvitation()
    {
        // The D4 guardrail's general rule (layer2-services.md §"Notification Generation"): 26 is
        // null-sourced (D5), so drop-self no longer keeps the canonizing moderator out of the fan-out
        // when they used the name themselves — the call site must. Their adoption state is still
        // stamped, as it was before D5.
        string name = $"Self{Guid.NewGuid():N}"[..12];
        await SeedClusterStoryAsync(_modId, name);
        await SeedClusterStoryAsync(_authorA, name);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using (IServiceScope scope = NewScope())
        {
            IFanonWriteService fanon = scope.ServiceProvider.GetRequiredService<IFanonWriteService>();
            await fanon.LinkGroupAsync(new FanonLinkCreateDto(name, _baseTagId, _targetTagId));
        }

        using IServiceScope verify = NewScope();
        ApplicationDbContext db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        List<int> invited = await db.Notifications
            .Where(n => n.NotificationTypeId == NotificationTypeEnum.TagUpdateSuggestion)
            .Select(n => n.RecipientUserId).ToListAsync();
        invited.Should().Equal([_authorA], "the moderator gets no invitation about their own act");
        (await db.TagAdoptionStates.AnyAsync(s => s.UserId == _modId && s.TargetTagId == _targetTagId
            && s.DateNotified != null)).Should().BeTrue("the never-twice record still covers the moderator");
    }

    [Fact]
    public async Task LinkGroupAsync_DuplicateLink_Throws()
    {
        string name = $"Dup{Guid.NewGuid():N}"[..12];
        await SeedClusterStoryAsync(_authorA, name);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using IServiceScope scope = NewScope();
        IFanonWriteService fanon = scope.ServiceProvider.GetRequiredService<IFanonWriteService>();
        await fanon.LinkGroupAsync(new FanonLinkCreateDto(name, _baseTagId, _targetTagId));

        Func<Task> act = () => fanon.LinkGroupAsync(new FanonLinkCreateDto(name.ToUpperInvariant(), _baseTagId, _targetTagId));
        await act.Should().ThrowAsync<TagValidationException>("the group key is normalized — casing doesn't make a new group");
    }

    // ── Adoption ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task AdoptAll_MutatesInPlace_PreservingNuancePriorityAndPairings()
    {
        string name = $"Adpt{Guid.NewGuid():N}"[..12];
        int storyId = await SeedClusterStoryAsync(_authorA, name, nuance: "the note survives");

        // Give the OC row a pairing partner so pairing survival is provable.
        int partnerRowId;
        int ocRowId;
        using (IServiceScope scope = NewScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            StoryCharacter ocRow = await db.StoryCharacters.SingleAsync(sc => sc.StoryId == storyId);
            ocRowId = ocRow.StoryCharacterId;
            StoryCharacter partner = new()
            {
                StoryId = storyId, CharacterTagId = _baseTagId, Priority = TagPriority.Primary
            };
            db.StoryCharacters.Add(partner);
            await db.SaveChangesAsync();
            partnerRowId = partner.StoryCharacterId;
            db.StoryCharacterPairings.Add(new StoryCharacterPairing
            {
                StoryId = storyId, PairingType = CharacterPairingType.Platonic, Priority = TagPriority.Primary,
                Members =
                {
                    new StoryCharacterPairingMember { StoryCharacterId = ocRowId },
                    new StoryCharacterPairingMember { StoryCharacterId = partnerRowId },
                }
            });
            await db.SaveChangesAsync();
        }

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using (IServiceScope scope = NewScope())
            await scope.ServiceProvider.GetRequiredService<IFanonWriteService>()
                .LinkGroupAsync(new FanonLinkCreateDto(name, _baseTagId, _targetTagId));

        SetActiveUser(_authorA);
        AdoptResultDto result;
        using (IServiceScope scope = NewScope())
            result = await scope.ServiceProvider.GetRequiredService<IFanonWriteService>().AdoptAllAsync(_targetTagId);

        result.Adopted.Should().Be(1);
        result.SkippedCollisions.Should().Be(0);

        using (IServiceScope scope = NewScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            StoryCharacter adopted = await db.StoryCharacters.SingleAsync(sc => sc.StoryCharacterId == ocRowId);
            adopted.CharacterTagId.Should().Be(_targetTagId, "naming moves to the tag");
            adopted.IsOc.Should().BeFalse();
            adopted.CustomName.Should().BeNull();
            adopted.Nuance.Should().Be("the note survives", "Nuance is never OC-scoped");
            adopted.Priority.Should().Be(TagPriority.Supporting, "priority survives");

            // The row id was stable, so the pairing membership survived untouched.
            (await db.StoryCharacterPairingMembers.CountAsync(m => m.StoryCharacterId == ocRowId))
                .Should().Be(1, "pairings survive adoption (stable StoryCharacterId)");
        }
    }

    [Fact]
    public async Task Adopt_StoryAlreadyCarryingTargetTag_SkipsWithExplanation()
    {
        string name = $"Coll{Guid.NewGuid():N}"[..12];
        int storyId = await SeedClusterStoryAsync(_authorA, name);
        using (IServiceScope scope = NewScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.StoryCharacters.Add(new StoryCharacter { StoryId = storyId, CharacterTagId = _targetTagId });
            await db.SaveChangesAsync();
        }

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using (IServiceScope scope = NewScope())
            await scope.ServiceProvider.GetRequiredService<IFanonWriteService>()
                .LinkGroupAsync(new FanonLinkCreateDto(name, _baseTagId, _targetTagId));

        SetActiveUser(_authorA);
        AdoptResultDto result;
        using (IServiceScope scope = NewScope())
            result = await scope.ServiceProvider.GetRequiredService<IFanonWriteService>().AdoptAsync(_targetTagId, storyId);

        result.Adopted.Should().Be(0);
        result.SkippedCollisions.Should().Be(1, "collisions skip, never merge");

        using IServiceScope verify = NewScope();
        ApplicationDbContext verifyDb = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await verifyDb.StoryCharacters.SingleAsync(sc => sc.StoryId == storyId && sc.CharacterTagId == _baseTagId))
            .CustomName.Should().Be(name, "the skipped row is untouched");
    }

    [Fact]
    public async Task Adopt_StoryWithCaseVariantDuplicates_SkipsInsteadOfCrashing()
    {
        // Regression: the group key is case-INSENSITIVE but the DB unique index is
        // case-SENSITIVE, so one story can hold "Saura" AND "saura" on one base tag and both
        // match the same link. Adopting both would write two identical (story, target, NULL)
        // rows — a raw DbUpdateException (500), not the friendly collision skip.
        string name = $"Case{Guid.NewGuid():N}"[..12];
        int storyId = await SeedClusterStoryAsync(_authorA, name);
        using (IServiceScope scope = NewScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.StoryCharacters.Add(new StoryCharacter
            {
                StoryId = storyId, CharacterTagId = _baseTagId, IsOc = true,
                CustomName = name.ToLowerInvariant()   // legal: index is case-sensitive
            });
            await db.SaveChangesAsync();
        }

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using (IServiceScope scope = NewScope())
            await scope.ServiceProvider.GetRequiredService<IFanonWriteService>()
                .LinkGroupAsync(new FanonLinkCreateDto(name, _baseTagId, _targetTagId));

        SetActiveUser(_authorA);
        AdoptResultDto result;
        using (IServiceScope scope = NewScope())
            result = await scope.ServiceProvider.GetRequiredService<IFanonWriteService>().AdoptAllAsync(_targetTagId);

        result.Adopted.Should().Be(0, "a story holding case-variant duplicates is skipped whole");
        result.SkippedCollisions.Should().Be(2, "both rows report as skipped, not adopted");

        using IServiceScope verify = NewScope();
        ApplicationDbContext verifyDb = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await verifyDb.StoryCharacters.CountAsync(sc => sc.StoryId == storyId && sc.CharacterTagId == _baseTagId))
            .Should().Be(2, "both original rows are untouched");
    }

    // ── Adoption pages + dismissal ─────────────────────────────────────────────

    [Fact]
    public async Task AdoptionPage_ListsRows_AndDismissIsReversible()
    {
        string name = $"Page{Guid.NewGuid():N}"[..12];
        await SeedClusterStoryAsync(_authorA, name);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using (IServiceScope scope = NewScope())
            await scope.ServiceProvider.GetRequiredService<IFanonWriteService>()
                .LinkGroupAsync(new FanonLinkCreateDto(name, _baseTagId, _targetTagId));

        SetActiveUser(_authorA);
        using (IServiceScope scope = NewScope())
        {
            IFanonReadService fanon = scope.ServiceProvider.GetRequiredService<IFanonReadService>();
            TagAdoptionPageDto? page = await fanon.GetMyAdoptionPageAsync(_targetTagId);
            page.Should().NotBeNull();
            page!.Rows.Should().ContainSingle(r => r.CustomName == name && !r.Collides);
            page.IsDismissed.Should().BeFalse();

            IReadOnlyList<MyTagAdoptionSummaryDto> index = await fanon.GetMyAdoptionIndexAsync();
            index.Should().ContainSingle(s => s.TargetTag.TagId == _targetTagId && s.PendingRowCount == 1);
        }

        using (IServiceScope scope = NewScope())
            await scope.ServiceProvider.GetRequiredService<IFanonWriteService>().SetDismissedAsync(_targetTagId, true);
        using (IServiceScope scope = NewScope())
        {
            (await scope.ServiceProvider.GetRequiredService<IFanonReadService>().GetMyAdoptionPageAsync(_targetTagId))!
                .IsDismissed.Should().BeTrue();
        }
        using (IServiceScope scope = NewScope())
            await scope.ServiceProvider.GetRequiredService<IFanonWriteService>().SetDismissedAsync(_targetTagId, false);
        using (IServiceScope scope = NewScope())
        {
            (await scope.ServiceProvider.GetRequiredService<IFanonReadService>().GetMyAdoptionPageAsync(_targetTagId))!
                .IsDismissed.Should().BeFalse("dismissal is reversible");
        }
    }

    // ── Editor nudge resolution ────────────────────────────────────────────────

    [Fact]
    public async Task FindOfficialTagByName_ResolvesLinkedGroupName_ToDisambiguatedTarget()
    {
        string name = $"Ndge{Guid.NewGuid():N}"[..12];
        await SeedClusterStoryAsync(_authorA, name);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using (IServiceScope scope = NewScope())
            await scope.ServiceProvider.GetRequiredService<IFanonWriteService>()
                .LinkGroupAsync(new FanonLinkCreateDto(name, _baseTagId, _targetTagId));

        SetActiveUser(_authorB);
        using IServiceScope readScope = NewScope();
        IFanonReadService fanon = readScope.ServiceProvider.GetRequiredService<IFanonReadService>();

        TagChipDto? match = await fanon.FindOfficialTagByNameAsync(TagTypeEnum.Character, name.ToUpperInvariant());
        match.Should().NotBeNull("the link resolves the group name even though the tag's own name carries a disambiguator");
        match!.TagId.Should().Be(_targetTagId);
    }
}
