using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="IPollWriteService"/> / <see cref="IPollReadService"/> and
/// <see cref="PollEditNotificationSweeper"/> (Feature 37, WU-Polls; settled 2026-07-12 —
/// audit/BlogPosts.md F37).
///
/// <b>Seeding plan (CLAUDE.md Phase-4 rules):</b>
/// <list type="bullet">
///   <item><description><b>Per-test seeding:</b> <c>InitializeAsync</c> seeds four users via
///   <c>SeedUserAsync</c> — a moderator (site-poll owner), a blog author, and two voters. Never
///   queried by name; ids only.</description></item>
///   <item><description><b>FK parents:</b> <c>SitePoll.OwnerId</c> ← the seeded moderator.
///   <c>BlogPostPoll.BlogPostId</c> ← a <c>ProfileBlogPost</c> seeded inline via
///   <c>ApplicationDbContext</c> (<see cref="SeedBlogPostAsync"/>) with the seeded author as
///   FK parent. Votes reference options created by the service itself.</description></item>
///   <item><description><b>Count-sensitive:</b> none — polls have no numeric limits beyond
///   min-2 options (validated, not counted).</description></item>
/// </list>
/// Tier: Integration (Testcontainers Postgres; Respawn reset per test; PollEditNotificationWorker
/// removed by TestAppFactory — tests drive the sweeper directly).
/// </summary>
[Collection("Postgres")]
public class PollServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _modId;
    private int _authorId;
    private int _voterId;
    private int _voter2Id;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _modId    = await SeedUserAsync("Mod");
        _authorId = await SeedUserAsync("Author");
        _voterId  = await SeedUserAsync("Voter");
        _voter2Id = await SeedUserAsync("Voter2");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private IServiceScope NewScope() => Factory.Services.CreateScope();

    private static PollEditDto NewDto(
        string name = "Favorite starter?",
        bool allowMultiple = false,
        PollResultsVisibility visibility = PollResultsVisibility.Always,
        PollAnonymityMode anonymity = PollAnonymityMode.Anonymous,
        DateTime? opened = null,
        DateTime? closed = null,
        params string[] options) => new()
    {
        PollName = name,
        AllowMultiple = allowMultiple,
        ResultsVisibility = visibility,
        AnonymityMode = anonymity,
        DateOpened = opened,
        DateClosed = closed,
        Options = (options.Length > 0 ? options : ["Turtwig", "Chimchar", "Piplup"])
            .Select(t => new PollOptionEditDto(null, t)).ToList(),
    };

    /// <summary>FK parent for BlogPostPolls — minimal published profile blog post.</summary>
    private async Task<int> SeedBlogPostAsync(int authorId)
    {
        using IServiceScope scope = NewScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ProfileBlogPost post = new()
        {
            AuthorId = authorId,
            Title = "Test post",
            Content = "<p>hi</p>",
            Rating = Rating.E,
            IsPublished = true,
            DateCreated = DateTime.UtcNow,
            LastUpdatedDate = DateTime.UtcNow,
        };
        db.BlogPosts.Add(post);
        await db.SaveChangesAsync();
        return post.BlogPostId;
    }

    private async Task<int> CreateSitePollAsync(PollEditDto? dto = null)
    {
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using IServiceScope scope = NewScope();
        IPollWriteService service = scope.ServiceProvider.GetRequiredService<IPollWriteService>();
        return await service.CreateSitePollAsync(dto ?? NewDto());
    }

    private async Task<PollDto> GetPollAsync(int pollId, int? asUserId, bool asMod = false)
    {
        SetActiveUser(asUserId is int uid
            ? asMod ? FakeActiveUserContext.Moderator(uid) : FakeActiveUserContext.AuthenticatedUser(uid, false)
            : FakeActiveUserContext.Anonymous());
        using IServiceScope scope = NewScope();
        IPollReadService service = scope.ServiceProvider.GetRequiredService<IPollReadService>();
        PollDto? poll = await service.GetPollAsync(pollId);
        poll.Should().NotBeNull();
        return poll!;
    }

    private async Task<PollDto> VoteAsync(int pollId, int userId, int[] optionIds, bool anonymously = false)
    {
        SetActiveUser(userId);
        using IServiceScope scope = NewScope();
        IPollWriteService service = scope.ServiceProvider.GetRequiredService<IPollWriteService>();
        return await service.VoteAsync(pollId, optionIds, anonymously);
    }

    // ── Create: permissions + persisted config ───────────────────────────────────

    [Fact]
    public async Task CreateSitePoll_AsModerator_PersistsConfig()
    {
        int pollId = await CreateSitePollAsync(NewDto(
            allowMultiple: true,
            visibility: PollResultsVisibility.AfterClose,
            anonymity: PollAnonymityMode.VoterChoice));

        PollDto poll = await GetPollAsync(pollId, _voterId);
        poll.AllowMultiple.Should().BeTrue();
        poll.ResultsVisibility.Should().Be(PollResultsVisibility.AfterClose);
        poll.AnonymityMode.Should().Be(PollAnonymityMode.VoterChoice);
        poll.OwnerId.Should().Be(_modId);
        poll.Status.Should().Be(PollStatus.Open);          // null DateOpened = open immediately
        poll.DateClosed.Should().BeNull();                  // indefinite
        poll.Options.Should().HaveCount(3);
        poll.Options.Select(o => o.SortOrder).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task CreateSitePoll_AsRegularUser_Throws()
    {
        SetActiveUser(_voterId);
        using IServiceScope scope = NewScope();
        IPollWriteService service = scope.ServiceProvider.GetRequiredService<IPollWriteService>();

        Func<Task> act = () => service.CreateSitePollAsync(NewDto());
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task CreateBlogPostPoll_AsAuthor_Creates_AsOther_Throws()
    {
        int blogPostId = await SeedBlogPostAsync(_authorId);

        SetActiveUser(_authorId);
        using (IServiceScope scope = NewScope())
        {
            IPollWriteService service = scope.ServiceProvider.GetRequiredService<IPollWriteService>();
            int pollId = await service.CreateBlogPostPollAsync(blogPostId, NewDto());
            PollDto poll = await GetPollAsync(pollId, _authorId);
            poll.BlogPostId.Should().Be(blogPostId);
        }

        SetActiveUser(_voterId);
        using (IServiceScope scope = NewScope())
        {
            IPollWriteService service = scope.ServiceProvider.GetRequiredService<IPollWriteService>();
            Func<Task> act = () => service.CreateBlogPostPollAsync(blogPostId, NewDto());
            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }
    }

    [Fact]
    public async Task CreatePoll_FewerThanTwoOptions_ThrowsValidation()
    {
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using IServiceScope scope = NewScope();
        IPollWriteService service = scope.ServiceProvider.GetRequiredService<IPollWriteService>();

        Func<Task> act = () => service.CreateSitePollAsync(NewDto(options: ["Only one"]));
        await act.Should().ThrowAsync<PollValidationException>();
    }

    // ── List queries (regression net for the SitePoll/BlogPostPoll coercion bug —
    //    a child-typed OfType source broke ProjectAsync's cross-child casts; found via
    //    browser verification 2026-07-12) ─────────────────────────────────────────

    [Fact]
    public async Task GetSitePolls_FiltersArchived_AndListsBoth()
    {
        int activeId = await CreateSitePollAsync(NewDto("Active poll"));
        int archivedId = await CreateSitePollAsync(NewDto("Archived poll"));

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using (IServiceScope scope = NewScope())
        {
            await scope.ServiceProvider.GetRequiredService<IPollWriteService>()
                .SetSitePollArchivedAsync(archivedId, true);
        }

        SetActiveUser(_voterId);
        using IServiceScope readScope = NewScope();
        IPollReadService read = readScope.ServiceProvider.GetRequiredService<IPollReadService>();

        PollDto[] activeOnly = await read.GetSitePollsAsync(includeArchived: false);
        activeOnly.Select(p => p.PollId).Should().Contain(activeId).And.NotContain(archivedId);

        PollDto[] all = await read.GetSitePollsAsync(includeArchived: true);
        all.Select(p => p.PollId).Should().Contain([activeId, archivedId]);
        all.Single(p => p.PollId == archivedId).IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task GetPollsForBlogPost_ReturnsOnlyThatPostsPolls()
    {
        int blogPostId = await SeedBlogPostAsync(_authorId);
        int otherPostId = await SeedBlogPostAsync(_authorId);
        await CreateSitePollAsync(); // site poll must NOT leak into the blog list

        SetActiveUser(_authorId);
        int pollId;
        using (IServiceScope scope = NewScope())
        {
            IPollWriteService service = scope.ServiceProvider.GetRequiredService<IPollWriteService>();
            pollId = await service.CreateBlogPostPollAsync(blogPostId, NewDto("Blog poll"));
            await service.CreateBlogPostPollAsync(otherPostId, NewDto("Other post's poll"));
        }

        SetActiveUser(_voterId);
        using IServiceScope readScope = NewScope();
        IPollReadService read = readScope.ServiceProvider.GetRequiredService<IPollReadService>();

        PollDto[] polls = await read.GetPollsForBlogPostAsync(blogPostId);
        polls.Should().ContainSingle(p => p.PollId == pollId);
        polls.Should().OnlyContain(p => p.BlogPostId == blogPostId);
    }

    // ── Voting ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Vote_SingleChoice_ReplacesPriorVote()
    {
        int pollId = await CreateSitePollAsync();
        PollDto poll = await GetPollAsync(pollId, _voterId);
        int optionA = poll.Options[0].PollOptionId;
        int optionB = poll.Options[1].PollOptionId;

        PollDto after = await VoteAsync(pollId, _voterId, [optionA]);
        after.ViewerVotedOptionIds.Should().Equal(optionA);

        after = await VoteAsync(pollId, _voterId, [optionB]);
        after.ViewerVotedOptionIds.Should().Equal(optionB);
        after.Options.Single(o => o.PollOptionId == optionA).VoteCount.Should().Be(0);
        after.Options.Single(o => o.PollOptionId == optionB).VoteCount.Should().Be(1);
    }

    [Fact]
    public async Task Vote_SingleChoice_TwoOptions_ThrowsValidation()
    {
        int pollId = await CreateSitePollAsync();
        PollDto poll = await GetPollAsync(pollId, _voterId);

        Func<Task> act = () => VoteAsync(pollId, _voterId,
            [poll.Options[0].PollOptionId, poll.Options[1].PollOptionId]);
        await act.Should().ThrowAsync<PollValidationException>();
    }

    [Fact]
    public async Task Vote_MultiChoice_AcceptsSeveral_AndRetractAll()
    {
        int pollId = await CreateSitePollAsync(NewDto(allowMultiple: true));
        PollDto poll = await GetPollAsync(pollId, _voterId);
        int[] picks = [poll.Options[0].PollOptionId, poll.Options[2].PollOptionId];

        PollDto after = await VoteAsync(pollId, _voterId, picks);
        after.ViewerVotedOptionIds.Should().BeEquivalentTo(picks);
        after.TotalVoterCount.Should().Be(1); // one distinct voter, two option rows

        after = await VoteAsync(pollId, _voterId, []); // retract everything
        after.ViewerVotedOptionIds.Should().BeEmpty();
        after.TotalVoterCount.Should().Be(0);
    }

    [Fact]
    public async Task Vote_OnPendingPoll_Throws()
    {
        int pollId = await CreateSitePollAsync(NewDto(opened: DateTime.UtcNow.AddDays(1)));
        PollDto poll = await GetPollAsync(pollId, _voterId, asMod: false);
        poll.Status.Should().Be(PollStatus.Pending);

        Func<Task> act = async () =>
        {
            PollDto asMod = await GetPollAsync(pollId, _modId, asMod: true);
            await VoteAsync(pollId, _voterId, [asMod.Options[0].PollOptionId]);
        };
        await act.Should().ThrowAsync<PollValidationException>();
    }

    [Fact]
    public async Task Vote_OnClosedPoll_Throws()
    {
        int pollId = await CreateSitePollAsync();
        PollDto poll = await GetPollAsync(pollId, _voterId);
        int optionId = poll.Options[0].PollOptionId;

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using (IServiceScope scope = NewScope())
        {
            await scope.ServiceProvider.GetRequiredService<IPollWriteService>().ClosePollAsync(pollId);
        }

        Func<Task> act = () => VoteAsync(pollId, _voterId, [optionId]);
        await act.Should().ThrowAsync<PollValidationException>();
    }

    // ── Results visibility + anonymity (server-side enforcement) ─────────────────

    [Fact]
    public async Task AfterVoteVisibility_HidesCountsFromNonVoter_ShowsAfterVote_HidesAfterRetract()
    {
        int pollId = await CreateSitePollAsync(NewDto(visibility: PollResultsVisibility.AfterVote));
        PollDto asMod = await GetPollAsync(pollId, _modId, asMod: true);
        int optionId = asMod.Options[0].PollOptionId;
        await VoteAsync(pollId, _voter2Id, [optionId]); // someone else's vote to hide/reveal

        // Non-voter: zeroed server-side.
        PollDto poll = await GetPollAsync(pollId, _voterId);
        poll.ResultsVisibleToViewer.Should().BeFalse();
        poll.TotalVoterCount.Should().Be(0);
        poll.Options.Should().OnlyContain(o => o.VoteCount == 0);

        // After voting: visible.
        PollDto after = await VoteAsync(pollId, _voterId, [optionId]);
        after.ResultsVisibleToViewer.Should().BeTrue();
        after.Options.Single(o => o.PollOptionId == optionId).VoteCount.Should().Be(2);

        // Retract: hidden again (settled 2026-07-12 — pure function of current state).
        PollDto retracted = await VoteAsync(pollId, _voterId, []);
        retracted.ResultsVisibleToViewer.Should().BeFalse();
        retracted.Options.Should().OnlyContain(o => o.VoteCount == 0);
    }

    [Fact]
    public async Task AnonymousMode_NeverExposesVoterNames()
    {
        int pollId = await CreateSitePollAsync(NewDto(anonymity: PollAnonymityMode.Anonymous));
        PollDto poll = await GetPollAsync(pollId, _voterId);
        await VoteAsync(pollId, _voterId, [poll.Options[0].PollOptionId]);

        PollDto asOther = await GetPollAsync(pollId, _voter2Id);
        asOther.Options.Should().OnlyContain(o => o.PublicVoters.Length == 0);
    }

    [Fact]
    public async Task VoterChoiceMode_HonorsPerVoterOptOut()
    {
        int pollId = await CreateSitePollAsync(NewDto(anonymity: PollAnonymityMode.VoterChoice));
        PollDto poll = await GetPollAsync(pollId, _voterId);
        int optionId = poll.Options[0].PollOptionId;

        await VoteAsync(pollId, _voterId, [optionId], anonymously: false);  // public
        await VoteAsync(pollId, _voter2Id, [optionId], anonymously: true);  // opted out

        PollDto result = await GetPollAsync(pollId, _authorId);
        PollOptionResultDto option = result.Options.Single(o => o.PollOptionId == optionId);
        option.VoteCount.Should().Be(2);
        option.PublicVoters.Should().ContainSingle(v => v.UserId == _voterId);
        option.PublicVoters.Should().NotContain(v => v.UserId == _voter2Id);

        PollDto voter2View = await GetPollAsync(pollId, _voter2Id);
        voter2View.ViewerVotedAnonymously.Should().BeTrue();
    }

    // ── Update: config lock + option reconcile + LastEditedAt stamping ───────────

    [Fact]
    public async Task Update_BeforeVotes_ChangesConfig()
    {
        int pollId = await CreateSitePollAsync(NewDto(allowMultiple: false));

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using IServiceScope scope = NewScope();
        IPollWriteService service = scope.ServiceProvider.GetRequiredService<IPollWriteService>();
        await service.UpdatePollAsync(pollId, NewDto(allowMultiple: true));

        PollDto poll = await GetPollAsync(pollId, _voterId);
        poll.AllowMultiple.Should().BeTrue();
        poll.ConfigLocked.Should().BeFalse();
    }

    [Fact]
    public async Task Update_ConfigChangeAfterVotes_ThrowsLockError()
    {
        int pollId = await CreateSitePollAsync(NewDto(allowMultiple: false));
        PollDto poll = await GetPollAsync(pollId, _voterId);
        await VoteAsync(pollId, _voterId, [poll.Options[0].PollOptionId]);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using IServiceScope scope = NewScope();
        IPollWriteService service = scope.ServiceProvider.GetRequiredService<IPollWriteService>();

        Func<Task> act = () => service.UpdatePollAsync(pollId, NewDto(allowMultiple: true));
        await act.Should().ThrowAsync<PollValidationException>()
            .Where(e => e.Message.Contains("locked"));
    }

    [Fact]
    public async Task Update_OptionReconcile_KeepsVotesOnRetained_DropsVotesOnDeleted()
    {
        int pollId = await CreateSitePollAsync();
        PollDto poll = await GetPollAsync(pollId, _voterId);
        int keepId = poll.Options[0].PollOptionId;   // "Turtwig" — voter votes here
        int dropId = poll.Options[2].PollOptionId;   // "Piplup" — voter2 votes here, then deleted
        await VoteAsync(pollId, _voterId, [keepId]);
        await VoteAsync(pollId, _voter2Id, [dropId]);

        // Rename retained, delete Piplup, add a new option, reverse order.
        PollEditDto dto = NewDto();
        dto.Options =
        [
            new PollOptionEditDto(null, "Eevee"),                       // new, index 0
            new PollOptionEditDto(poll.Options[1].PollOptionId, "Chimchar"),
            new PollOptionEditDto(keepId, "Turtwig (the best)"),        // renamed, moved last
        ];

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using (IServiceScope scope = NewScope())
        {
            await scope.ServiceProvider.GetRequiredService<IPollWriteService>()
                .UpdatePollAsync(pollId, dto);
        }

        PollDto updated = await GetPollAsync(pollId, _modId, asMod: true);
        updated.Options.Should().HaveCount(3);
        updated.Options.Select(o => o.Text).Should()
            .ContainInOrder("Eevee", "Chimchar", "Turtwig (the best)");
        updated.Options.Single(o => o.PollOptionId == keepId).VoteCount.Should().Be(1); // vote survived rename+move
        updated.Options.Should().NotContain(o => o.PollOptionId == dropId);
        updated.TotalVoterCount.Should().Be(1);   // voter2's vote cascaded away with Piplup
    }

    [Fact]
    public async Task Update_MaterialEditWithVotes_StampsLastEditedAt_ReorderOnlyDoesNot()
    {
        int pollId = await CreateSitePollAsync();
        PollDto poll = await GetPollAsync(pollId, _voterId);
        await VoteAsync(pollId, _voterId, [poll.Options[0].PollOptionId]);

        // Reorder-only update (same ids, same texts, new order) — NOT material.
        PollEditDto reorder = NewDto();
        reorder.Options = poll.Options.Reverse()
            .Select(o => new PollOptionEditDto(o.PollOptionId, o.Text)).ToList();

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using (IServiceScope scope = NewScope())
        {
            await scope.ServiceProvider.GetRequiredService<IPollWriteService>().UpdatePollAsync(pollId, reorder);
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Polls.Where(p => p.PollId == pollId).Select(p => p.LastEditedAt).SingleAsync())
                .Should().BeNull("reorder alone is not a material edit");
        }

        // Rename — material.
        PollEditDto rename = NewDto();
        rename.Options = poll.Options.OrderBy(o => o.SortOrder)
            .Select((o, i) => new PollOptionEditDto(o.PollOptionId, i == 0 ? "Renamed!" : o.Text)).ToList();

        using (IServiceScope scope = NewScope())
        {
            await scope.ServiceProvider.GetRequiredService<IPollWriteService>().UpdatePollAsync(pollId, rename);
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Polls.Where(p => p.PollId == pollId).Select(p => p.LastEditedAt).SingleAsync())
                .Should().NotBeNull("a rename is a material edit and must arm the sweep");
        }
    }

    // ── Close / archive / delete ─────────────────────────────────────────────────

    [Fact]
    public async Task Close_StampsDateClosed_And_ArchiveIsOrthogonal()
    {
        int pollId = await CreateSitePollAsync();

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using (IServiceScope scope = NewScope())
        {
            IPollWriteService service = scope.ServiceProvider.GetRequiredService<IPollWriteService>();
            await service.ClosePollAsync(pollId);
            await service.SetSitePollArchivedAsync(pollId, true);
        }

        PollDto poll = await GetPollAsync(pollId, _voterId);
        poll.Status.Should().Be(PollStatus.Closed);
        poll.IsArchived.Should().BeTrue();

        // Archived-but-open is equally legal — unarchive and verify the flag flips alone.
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using (IServiceScope scope = NewScope())
        {
            await scope.ServiceProvider.GetRequiredService<IPollWriteService>()
                .SetSitePollArchivedAsync(pollId, false);
        }
        (await GetPollAsync(pollId, _voterId)).IsArchived.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_CascadesOptionsAndVotes()
    {
        int pollId = await CreateSitePollAsync();
        PollDto poll = await GetPollAsync(pollId, _voterId);
        await VoteAsync(pollId, _voterId, [poll.Options[0].PollOptionId]);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using (IServiceScope scope = NewScope())
        {
            await scope.ServiceProvider.GetRequiredService<IPollWriteService>().DeletePollAsync(pollId);
        }

        using (IServiceScope scope = NewScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Polls.AnyAsync(p => p.PollId == pollId)).Should().BeFalse();
            (await db.PollOptions.AnyAsync(o => o.PollId == pollId)).Should().BeFalse();
            (await db.PollVotes.AnyAsync()).Should().BeFalse();
        }
    }

    [Fact]
    public async Task BlogPoll_Manage_NonOwnerThrows()
    {
        int blogPostId = await SeedBlogPostAsync(_authorId);
        SetActiveUser(_authorId);
        int pollId;
        using (IServiceScope scope = NewScope())
        {
            pollId = await scope.ServiceProvider.GetRequiredService<IPollWriteService>()
                .CreateBlogPostPollAsync(blogPostId, NewDto());
        }

        SetActiveUser(_voterId);
        using (IServiceScope scope = NewScope())
        {
            IPollWriteService service = scope.ServiceProvider.GetRequiredService<IPollWriteService>();
            Func<Task> act = () => service.ClosePollAsync(pollId);
            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }
    }

    // ── Edit-notification sweep (PollEditNotificationSweeper) ────────────────────

    [Fact]
    public async Task Sweeper_QuietPeriodElapsed_NotifiesVotersOnce_NotOwner()
    {
        int blogPostId = await SeedBlogPostAsync(_authorId);
        SetActiveUser(_authorId);
        int pollId;
        using (IServiceScope scope = NewScope())
        {
            pollId = await scope.ServiceProvider.GetRequiredService<IPollWriteService>()
                .CreateBlogPostPollAsync(blogPostId, NewDto());
        }

        PollDto poll = await GetPollAsync(pollId, _authorId);
        int optionId = poll.Options[0].PollOptionId;
        await VoteAsync(pollId, _voterId, [optionId]);
        await VoteAsync(pollId, _voter2Id, [optionId]);
        await VoteAsync(pollId, _authorId, [optionId]); // owner votes too — must be drop-self'd

        // Arm the sweep with an already-elapsed quiet period (bypasses the write service so the
        // test controls the clock).
        using (IServiceScope scope = NewScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Polls.Where(p => p.PollId == pollId)
                .ExecuteUpdateAsync(s => s.SetProperty(
                    p => p.LastEditedAt, DateTime.UtcNow - PollEditNotificationSweeper.QuietPeriod - TimeSpan.FromMinutes(1)));
        }

        using (IServiceScope scope = NewScope())
        {
            PollEditNotificationSweeper sweeper =
                scope.ServiceProvider.GetRequiredService<PollEditNotificationSweeper>();
            (await sweeper.SweepAsync()).Should().Be(1);

            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var notifications = await db.Notifications
                .Where(n => n.NotificationTypeId == NotificationTypeEnum.PollUpdated)
                .Select(n => new { n.RecipientUserId, n.RelatedEntityId })
                .ToListAsync();

            notifications.Select(n => n.RecipientUserId)
                .Should().BeEquivalentTo([_voterId, _voter2Id], "owner is drop-self'd");
            notifications.Should().OnlyContain(n => n.RelatedEntityId == blogPostId,
                "blog-post polls navigate to their post");

            (await db.Polls.Where(p => p.PollId == pollId).Select(p => p.EditNotifiedAt).SingleAsync())
                .Should().NotBeNull();

            // Idempotent: a second sweep with no new edit does nothing.
            (await sweeper.SweepAsync()).Should().Be(0);
            (await db.Notifications.CountAsync(n => n.NotificationTypeId == NotificationTypeEnum.PollUpdated))
                .Should().Be(2);
        }
    }

    [Fact]
    public async Task Sweeper_QuietPeriodNotElapsed_DoesNothing()
    {
        int pollId = await CreateSitePollAsync();
        PollDto poll = await GetPollAsync(pollId, _voterId);
        await VoteAsync(pollId, _voterId, [poll.Options[0].PollOptionId]);

        using (IServiceScope scope = NewScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Polls.Where(p => p.PollId == pollId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.LastEditedAt, DateTime.UtcNow)); // just edited
        }

        using (IServiceScope scope = NewScope())
        {
            PollEditNotificationSweeper sweeper =
                scope.ServiceProvider.GetRequiredService<PollEditNotificationSweeper>();
            (await sweeper.SweepAsync()).Should().Be(0, "the 30-minute quiet period hasn't elapsed");
        }
    }
}
