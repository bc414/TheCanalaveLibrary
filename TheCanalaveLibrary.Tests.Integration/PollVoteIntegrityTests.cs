using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;
using TheCanalaveLibrary.Server.Data.Configurations;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Owner ruling D23's poll restructure (WU-PollVoteIntegrity, 2026-10-13): single-choice is a database
/// constraint, and the <c>ConfigLocked</c> rider freezes <c>AllowMultiple</c> in the data
/// (<c>layer2-services.md</c> §"Check-then-act posture"; <c>audit/BlogPosts.md</c> F37).
/// <list type="bullet">
///   <item><description><b>Schema</b> — one raw statement per rule on its own connection, asserting the
///   SQLSTATE and the exact constraint name (<c>testing.md</c> §"A CHECK, FK or unique index is pinned
///   with one raw statement").</description></item>
///   <item><description><b>Service</b> — the competing write lands between <c>VoteAsync</c>'s (or
///   <c>UpdatePollAsync</c>'s) read and its write, through <see cref="InterleavingCommandInterceptor"/>
///   (<c>testing.md</c> §"Testing a check-then-act guard: interleave, don't race"). Every such test
///   asserts <c>interceptor.Fired</c>.</description></item>
/// </list>
/// <b>Seeding plan (CLAUDE.md Phase-4 rules):</b> <c>InitializeAsync</c> seeds a moderator (site-poll
/// owner) and two voters via <c>SeedUserAsync</c>. Polls are site polls created through the service (no
/// blog-post parent needed); options come from the service. Raw vote inserts name their poll, option and
/// the poll's mode, as the composite FKs require. No count-sensitive limits.
/// Tier: Integration (Testcontainers Postgres; Respawn reset per test).
/// </summary>
[Collection("Postgres")]
public class PollVoteIntegrityTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private const string OptionForeignKey = "fk_poll_votes_poll_options_poll_id_poll_option_id";

    private int _modId;
    private int _voterId;
    private int _voter2Id;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _modId    = await SeedUserAsync("Mod");
        _voterId  = await SeedUserAsync("Voter");
        _voter2Id = await SeedUserAsync("Voter2");
    }

    // ── Schema: the rules hold against a writer that skips the service ──────────

    [Fact]
    public async Task SingleChoice_ASecondLiveRowForTheSameVoter_IsRefusedByTheGuard()
    {
        (int pollId, int[] options) = await CreateSitePollAsync(allowMultiple: false);
        await InsertVoteAsync(pollId, options[0], _voterId, allowMultiple: false);

        await ExpectViolationAsync(VoteSql(pollId, options[1], _voterId, allowMultiple: false),
            PostgresErrorCodes.UniqueViolation, PollVoteConfiguration.SingleChoiceIndex);

        // Another voter is unaffected, and the guard is the poll id on a single-choice row.
        await InsertVoteAsync(pollId, options[1], _voter2Id, allowMultiple: false);
        (await GuardsAsync(pollId)).Should().OnlyContain(g => g == pollId);
    }

    [Fact]
    public async Task SingleChoice_AnonymizedRowsCoexist_OnOneOptionAndAcrossOptions()
    {
        // NULLS DISTINCT on the guard index: D13's anonymized votes (user_id NULL) are never one voter.
        (int pollId, int[] options) = await CreateSitePollAsync(allowMultiple: false);

        await InsertVoteAsync(pollId, options[0], userId: null, allowMultiple: false);
        await InsertVoteAsync(pollId, options[0], userId: null, allowMultiple: false);
        await InsertVoteAsync(pollId, options[1], userId: null, allowMultiple: false);

        (await GuardsAsync(pollId)).Should().HaveCount(3);
    }

    [Fact]
    public async Task MultiChoice_SeveralRowsForOneVoter_AreAllowed_AndTheOptionUserIndexStillHolds()
    {
        (int pollId, int[] options) = await CreateSitePollAsync(allowMultiple: true);
        await InsertVoteAsync(pollId, options[0], _voterId, allowMultiple: true);
        await InsertVoteAsync(pollId, options[1], _voterId, allowMultiple: true);

        (await GuardsAsync(pollId)).Should().HaveCount(2).And.OnlyContain(g => g == null,
            "the guard is NULL on a multi-choice row, so the unique index never sees it");
        await ExpectViolationAsync(VoteSql(pollId, options[0], _voterId, allowMultiple: true),
            PostgresErrorCodes.UniqueViolation, PollVoteConfiguration.OptionUserIndex);
    }

    [Fact]
    public async Task TheGuard_IsGenerated_AWriterCannotSetIt()
    {
        (int pollId, int[] options) = await CreateSitePollAsync(allowMultiple: false);

        Func<Task> act = () => ExecuteAsync(
            "INSERT INTO poll_votes (poll_id, poll_option_id, user_id, allow_multiple, is_anonymous, single_choice_poll_id) " +
            $"VALUES ({pollId}, {options[0]}, {_voterId}, false, false, NULL)");

        // 428C9 generated_always ("cannot insert a non-DEFAULT value"); Npgsql's PostgresErrorCodes has no name for it.
        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState
            .Should().Be("428C9", "a NULL guard on a single-choice row would bypass the rule");
    }

    [Fact]
    public async Task AllowMultiple_CannotChangeWhileAVoteReferencesIt_AndCanOnceNoneDoes()
    {
        (int pollId, int[] options) = await CreateSitePollAsync(allowMultiple: false);
        await InsertVoteAsync(pollId, options[0], _voterId, allowMultiple: false);

        string flip = $"UPDATE base_polls SET allow_multiple = true WHERE poll_id = {pollId}";
        await ExpectViolationAsync(flip, PostgresErrorCodes.ForeignKeyViolation, PollVoteConfiguration.PollModeForeignKey);

        await ExecuteAsync($"DELETE FROM poll_votes WHERE poll_id = {pollId}");
        await ExecuteAsync(flip);
        (await PollModeAsync(pollId)).Should().BeTrue("with no vote left, the mode is editable again");
    }

    [Fact]
    public async Task AVote_NamingAnotherPollsOption_OrAModeThePollDoesNotHave_IsRefused()
    {
        (int pollId, int[] options) = await CreateSitePollAsync(allowMultiple: false);
        (int otherPollId, _) = await CreateSitePollAsync(allowMultiple: false);

        await ExpectViolationAsync(VoteSql(otherPollId, options[0], _voterId, allowMultiple: false),
            PostgresErrorCodes.ForeignKeyViolation, OptionForeignKey);
        await ExpectViolationAsync(VoteSql(pollId, options[0], _voterId, allowMultiple: true),
            PostgresErrorCodes.ForeignKeyViolation, PollVoteConfiguration.PollModeForeignKey);
    }

    // ── Service: a vote that loses a race is a 400, never a 500 ─────────────────

    [Fact]
    public async Task Vote_SingleChoice_TheSameVotersOtherVoteLandsFirst_IsRefused_OneRowStands_AndTheContextIsClean()
    {
        // Two tabs: this call loaded no vote, then the other tab's vote on option B committed before this
        // call's INSERT of option A. Before D23 both rows landed (two live votes on a single-choice poll).
        (int pollId, int[] options) = await CreateSitePollAsync(allowMultiple: false);
        InterleavingCommandInterceptor interceptor = new(ConnectionString, "INSERT INTO poll_votes",
            interleavedSql: VoteSql(pollId, options[1], _voterId, allowMultiple: false), interceptReaders: true);

        SetActiveUser(_voterId);
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerPollWriteService svc, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerPollWriteService>(scope, ConnectionString, interceptor);
        await using (writeDb)
        {
            (await FluentActions.Awaiting(() => svc.VoteAsync(pollId, [options[0]], false))
                    .Should().ThrowAsync<PollValidationException>())
                .WithMessage(ServerPollWriteService.VoteRaceMessage);
            interceptor.Fired.Should().BeTrue();
            (await LiveVotesAsync(pollId, _voterId)).Should().Equal([options[1]], "the database kept one vote");

            // The same circuit retries: the refused row was detached, so the replace lands cleanly.
            PollDto? retried = await svc.VoteAsync(pollId, [options[0]], false);
            retried!.ViewerVotedOptionIds.Should().Equal([options[0]]);
        }
        (await LiveVotesAsync(pollId, _voterId)).Should().Equal([options[0]]);
    }

    [Fact]
    public async Task Vote_MultiChoice_TheSameVotersOtherPickLandsFirst_BothRowsStand()
    {
        // Multi-choice is untouched by the guard: the competing pick and this call's pick both land.
        (int pollId, int[] options) = await CreateSitePollAsync(allowMultiple: true);
        InterleavingCommandInterceptor interceptor = new(ConnectionString, "INSERT INTO poll_votes",
            interleavedSql: VoteSql(pollId, options[1], _voterId, allowMultiple: true), interceptReaders: true);

        SetActiveUser(_voterId);
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerPollWriteService svc, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerPollWriteService>(scope, ConnectionString, interceptor);
        await using (writeDb)
        {
            await FluentActions.Awaiting(() => svc.VoteAsync(pollId, [options[0]], false)).Should().NotThrowAsync();
        }
        interceptor.Fired.Should().BeTrue();
        (await LiveVotesAsync(pollId, _voterId)).Should().BeEquivalentTo([options[0], options[1]]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Vote_TheSameVotersIdenticalVoteLandsFirst_CountsAsCast_AndTheContextIsClean(bool allowMultiple)
    {
        // A double submit (two tabs, a client retry): this call loaded no vote, then the voter's other request
        // cast the same vote before this call's INSERT. The INSERT collides on the (option, user) index: the
        // only index a multi-choice row reaches, and on single choice the older of the two, so Postgres checks
        // it first. The vote stands exactly as asked, so the call succeeds (D23's create-create rows count a
        // racing duplicate as done).
        (int pollId, int[] options) = await CreateSitePollAsync(allowMultiple);
        InterleavingCommandInterceptor interceptor = new(ConnectionString, "INSERT INTO poll_votes",
            interleavedSql: VoteSql(pollId, options[0], _voterId, allowMultiple), interceptReaders: true);

        SetActiveUser(_voterId);
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerPollWriteService svc, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerPollWriteService>(scope, ConnectionString, interceptor);
        await using (writeDb)
        {
            PollDto? cast = await svc.VoteAsync(pollId, [options[0]], false);
            interceptor.Fired.Should().BeTrue();
            cast!.ViewerVotedOptionIds.Should().Equal([options[0]]);
            (await LiveVotesAsync(pollId, _voterId)).Should().Equal([options[0]], "the vote was cast once");

            // The refused INSERT was detached, so the circuit's next vote does not replay it.
            PollDto? changed = await svc.VoteAsync(pollId, [options[1]], false);
            changed!.ViewerVotedOptionIds.Should().Equal([options[1]]);
        }
        (await LiveVotesAsync(pollId, _voterId)).Should().Equal([options[1]]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Vote_TheSameVotersOtherTabVotesTheSameOptionWithTheOtherVisibility_IsRefused(bool allowMultiple)
    {
        // VoterChoice: the other tab voted for the same option anonymously, and this call asks for a public vote
        // on it. The INSERT collides on the (option, user) index, and the row that stands is not the one asked for.
        (int pollId, int[] options) = await CreateSitePollAsync(allowMultiple, PollAnonymityMode.VoterChoice);
        InterleavingCommandInterceptor interceptor = new(ConnectionString, "INSERT INTO poll_votes",
            interleavedSql: VoteSql(pollId, options[0], _voterId, allowMultiple, isAnonymous: true),
            interceptReaders: true);

        SetActiveUser(_voterId);
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerPollWriteService svc, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerPollWriteService>(scope, ConnectionString, interceptor);
        await using (writeDb)
        {
            (await FluentActions.Awaiting(() => svc.VoteAsync(pollId, [options[0]], false))
                    .Should().ThrowAsync<PollValidationException>())
                .WithMessage(ServerPollWriteService.VoteRaceMessage);
        }
        interceptor.Fired.Should().BeTrue();
        (await LiveVotesAsync(pollId, _voterId)).Should().Equal([options[0]]);
        (await VoteAnonymityAsync(pollId, _voterId)).Should().Equal([true], "the other tab's anonymous vote stands");
    }

    [Fact]
    public async Task Vote_TheOwnerChangesTheModeBeforeTheInsert_IsRefused_AndNoRowLands()
    {
        // The vote read AllowMultiple = false; the owner switched to multiple choice (legal: no votes yet)
        // before the INSERT. A row carrying the old mode is refused by the mode FK.
        (int pollId, int[] options) = await CreateSitePollAsync(allowMultiple: false);
        InterleavingCommandInterceptor interceptor = new(ConnectionString, "INSERT INTO poll_votes",
            interleavedSql: $"UPDATE base_polls SET allow_multiple = true WHERE poll_id = {pollId}", interceptReaders: true);

        SetActiveUser(_voterId);
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerPollWriteService svc, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerPollWriteService>(scope, ConnectionString, interceptor);
        await using (writeDb)
        {
            (await FluentActions.Awaiting(() => svc.VoteAsync(pollId, [options[0]], false))
                    .Should().ThrowAsync<PollValidationException>())
                .WithMessage(ServerPollWriteService.VoteRaceMessage);
        }
        interceptor.Fired.Should().BeTrue();
        (await LiveVotesAsync(pollId, _voterId)).Should().BeEmpty();
        (await PollModeAsync(pollId)).Should().BeTrue();
    }

    [Fact]
    public async Task Vote_TheOptionIsRemovedBeforeTheInsert_IsRefused_NotA500()
    {
        (int pollId, int[] options) = await CreateSitePollAsync(allowMultiple: false);
        InterleavingCommandInterceptor interceptor = new(ConnectionString, "INSERT INTO poll_votes",
            interleavedSql: $"DELETE FROM poll_options WHERE poll_option_id = {options[0]}", interceptReaders: true);

        SetActiveUser(_voterId);
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerPollWriteService svc, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerPollWriteService>(scope, ConnectionString, interceptor);
        await using (writeDb)
        {
            (await FluentActions.Awaiting(() => svc.VoteAsync(pollId, [options[0]], false))
                    .Should().ThrowAsync<PollValidationException>())
                .WithMessage(ServerPollWriteService.VoteRaceMessage);
        }
        interceptor.Fired.Should().BeTrue();
        (await LiveVotesAsync(pollId, _voterId)).Should().BeEmpty();
    }

    [Fact]
    public async Task Vote_TheVotersOwnRowIsRemovedBeforeTheChange_IsRefused_NotA500()
    {
        // Delete-delete: this call loaded the vote on A to replace it with B; the other tab retracted first,
        // so this call's DELETE affects no row (DbUpdateConcurrencyException).
        (int pollId, int[] options) = await CreateSitePollAsync(allowMultiple: false);
        await VoteAsync(pollId, _voterId, [options[0]]);
        InterleavingCommandInterceptor interceptor = new(ConnectionString, "DELETE FROM poll_votes",
            interleavedSql: $"DELETE FROM poll_votes WHERE poll_id = {pollId} AND user_id = {_voterId}",
            interceptReaders: true);

        SetActiveUser(_voterId);
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerPollWriteService svc, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerPollWriteService>(scope, ConnectionString, interceptor);
        await using (writeDb)
        {
            (await FluentActions.Awaiting(() => svc.VoteAsync(pollId, [options[1]], false))
                    .Should().ThrowAsync<PollValidationException>())
                .WithMessage(ServerPollWriteService.VoteRaceMessage);
        }
        interceptor.Fired.Should().BeTrue();
        (await LiveVotesAsync(pollId, _voterId)).Should().BeEmpty("the save rolled back as a whole");
    }

    [Fact]
    public async Task Vote_TheSameVotersOtherTabRetractsFirst_TheRetractionCountsAsDone()
    {
        // Both tabs retract: this call loaded the vote on A, the other tab's retraction committed first, and this
        // call's DELETE affects no row (DbUpdateConcurrencyException). The voter holds no vote, as asked.
        (int pollId, int[] options) = await CreateSitePollAsync(allowMultiple: false);
        await VoteAsync(pollId, _voterId, [options[0]]);
        InterleavingCommandInterceptor interceptor = new(ConnectionString, "DELETE FROM poll_votes",
            interleavedSql: $"DELETE FROM poll_votes WHERE poll_id = {pollId} AND user_id = {_voterId}",
            interceptReaders: true);

        SetActiveUser(_voterId);
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerPollWriteService svc, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerPollWriteService>(scope, ConnectionString, interceptor);
        await using (writeDb)
        {
            PollDto? retracted = await svc.VoteAsync(pollId, [], false);
            retracted!.ViewerVotedOptionIds.Should().BeEmpty();
        }
        interceptor.Fired.Should().BeTrue();
        (await LiveVotesAsync(pollId, _voterId)).Should().BeEmpty();
    }

    // ── Service: the ConfigLocked rider (a first vote racing a mode edit) ────────

    [Fact]
    public async Task UpdatePoll_AFirstVoteLandsBeforeTheModeChange_IsRefusedAsLocked_AndNothingIsWritten()
    {
        // The edit read hasVotes = false; a first vote committed before its mode UPDATE. Before the rider the
        // mode flipped under a cast vote; now the mode FK refuses it and the whole edit rolls back.
        (int pollId, int[] options) = await CreateSitePollAsync(allowMultiple: false);
        InterleavingCommandInterceptor interceptor = new(ConnectionString, "UPDATE base_polls",
            interleavedSql: VoteSql(pollId, options[0], _voterId, allowMultiple: false));

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerPollWriteService svc, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerPollWriteService>(scope, ConnectionString, interceptor);
        await using (writeDb)
        {
            (await FluentActions.Awaiting(() => svc.UpdatePollAsync(pollId, EditDto("Renamed", allowMultiple: true, options)))
                    .Should().ThrowAsync<PollValidationException>())
                .WithMessage(ServerPollWriteService.VoteModeLockedMessage);

            // The scope models the circuit. The refused edit's pending rename went with the detached poll, so
            // the circuit's next save (the moderator voting) cannot write it.
            writeDb.ChangeTracker.Entries().Should().BeEmpty("a refused edit leaves nothing pending on the context");
            await svc.VoteAsync(pollId, [options[1]], false);
        }
        interceptor.Fired.Should().BeTrue();
        (await PollModeAsync(pollId)).Should().BeFalse("the vote was cast under single choice");
        (await LiveVotesAsync(pollId, _voterId)).Should().Equal([options[0]]);
        (await LiveVotesAsync(pollId, _modId)).Should().Equal([options[1]]);
        using IServiceScope verify = Factory.Services.CreateScope();
        (await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>().Polls
                .Where(p => p.PollId == pollId).Select(p => p.PollName).SingleAsync())
            .Should().Be("Favorite starter?",
                "the rename rolled back with the refused mode change, and the circuit's next save did not replay it");
    }

    [Fact]
    public async Task UpdatePoll_ModeChangedTwiceThroughOneScope_TheSecondEditSeesTheFirst()
    {
        // The mode is written set-based (it is a key column). One scope models the circuit: without the
        // detach, the second call loads the tracked poll still holding single choice, sees no change and
        // writes nothing, leaving the poll on multiple choice.
        (int pollId, int[] options) = await CreateSitePollAsync(allowMultiple: false);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            IPollWriteService svc = scope.ServiceProvider.GetRequiredService<IPollWriteService>();
            await svc.UpdatePollAsync(pollId, EditDto("Favorite starter?", allowMultiple: true, options));
            (await PollModeAsync(pollId)).Should().BeTrue();

            await svc.UpdatePollAsync(pollId, EditDto("Favorite starter?", allowMultiple: false, options));
        }
        (await PollModeAsync(pollId)).Should().BeFalse();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<(int PollId, int[] OptionIds)> CreateSitePollAsync(
        bool allowMultiple, PollAnonymityMode anonymity = PollAnonymityMode.Public)
    {
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using IServiceScope scope = Factory.Services.CreateScope();
        int pollId = await scope.ServiceProvider.GetRequiredService<IPollWriteService>().CreateSitePollAsync(new PollEditDto
        {
            PollName = "Favorite starter?",
            AllowMultiple = allowMultiple,
            ResultsVisibility = PollResultsVisibility.Always,
            AnonymityMode = anonymity,
            Options = [new PollOptionEditDto(null, "Turtwig"), new PollOptionEditDto(null, "Chimchar")],
        });
        int[] options = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PollOptions
            .Where(o => o.PollId == pollId).OrderBy(o => o.SortOrder).Select(o => o.PollOptionId).ToArrayAsync();
        return (pollId, options);
    }

    /// <summary>An edit that keeps both options (by id) and sets the mode.</summary>
    private static PollEditDto EditDto(string name, bool allowMultiple, int[] options) => new()
    {
        PollName = name,
        AllowMultiple = allowMultiple,
        ResultsVisibility = PollResultsVisibility.Always,
        AnonymityMode = PollAnonymityMode.Public,
        Options = [new PollOptionEditDto(options[0], "Turtwig"), new PollOptionEditDto(options[1], "Chimchar")],
    };

    private async Task VoteAsync(int pollId, int userId, int[] optionIds)
    {
        SetActiveUser(userId);
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPollWriteService>().VoteAsync(pollId, optionIds, false);
    }

    private static string VoteSql(int pollId, int optionId, int? userId, bool allowMultiple, bool isAnonymous = false) =>
        "INSERT INTO poll_votes (poll_id, poll_option_id, user_id, allow_multiple, is_anonymous) " +
        $"VALUES ({pollId}, {optionId}, {(userId is int u ? u.ToString() : "NULL")}, " +
        $"{(allowMultiple ? "true" : "false")}, {(isAnonymous ? "true" : "false")})";

    private Task InsertVoteAsync(int pollId, int optionId, int? userId, bool allowMultiple) =>
        ExecuteAsync(VoteSql(pollId, optionId, userId, allowMultiple));

    private async Task<List<int?>> GuardsAsync(int pollId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PollVotes
            .Where(v => v.PollId == pollId).Select(v => v.SingleChoicePollId).ToListAsync();
    }

    private async Task<List<int>> LiveVotesAsync(int pollId, int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PollVotes
            .Where(v => v.PollId == pollId && v.UserId == userId)
            .OrderBy(v => v.PollOptionId).Select(v => v.PollOptionId).ToListAsync();
    }

    private async Task<List<bool>> VoteAnonymityAsync(int pollId, int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PollVotes
            .Where(v => v.PollId == pollId && v.UserId == userId)
            .OrderBy(v => v.PollOptionId).Select(v => v.IsAnonymous).ToListAsync();
    }

    private async Task<bool> PollModeAsync(int pollId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Polls
            .Where(p => p.PollId == pollId).Select(p => p.AllowMultiple).SingleAsync();
    }

    private async Task ExpectViolationAsync(string sql, string sqlState, string constraintName)
    {
        Func<Task> act = () => ExecuteAsync(sql);

        PostgresException ex = (await act.Should().ThrowAsync<PostgresException>()).Which;
        ex.SqlState.Should().Be(sqlState, sql);
        ex.ConstraintName.Should().Be(constraintName, sql);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using NpgsqlConnection conn = new(ConnectionString);
        await conn.OpenAsync();
        await using NpgsqlCommand cmd = new(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
