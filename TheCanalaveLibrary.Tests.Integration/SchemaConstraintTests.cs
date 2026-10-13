using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// The database's own guarantees, written around the service layer (WU-SchemaHardening, schema audit
/// §2.4/§2.6/§2.7): every CHECK in <c>layer1-data-model.md</c> §"CHECK constraints", the group-folder
/// self-FK and its NULLS NOT DISTINCT name index, <c>group_stories</c>' uniqueness (D23) and the filter
/// settings' natural key. Each test runs one raw statement that a writer which skipped the service
/// would run, and asserts the exact SQLSTATE and constraint name, so a later migration that drops or
/// renames one fails here. The inventory test compares the catalog's CHECK set with the documented one:
/// a nuke-and-rebuild that lost a constraint, or a new one nobody documented, fails it.
/// <para>
/// <b>Per-test seeding:</b> users via <c>SeedUserAsync</c>, stories via <c>SeedStoryAsync</c>, a group
/// and its folder via <see cref="IGroupWriteService"/> (the creator becomes Admin), tags inline through
/// <see cref="ApplicationDbContext"/> with a <c>HasData</c> tag type (the <c>FanonPipelineTests</c>
/// pattern). Lookup keys (lineage type 1, search modes, filter kinds) are <c>HasData</c> rows Respawn
/// keeps. Every violating statement runs on its own autocommit connection.
/// </para>
/// Tier: Integration (Testcontainers Postgres; the migrations under test are the schema).
/// </summary>
[Collection("Postgres")]
public class SchemaConstraintTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    /// <summary>The documented CHECK set — keep in step with layer1-data-model.md §"CHECK constraints".</summary>
    private static readonly string[] DocumentedChecks =
    [
        "ck_base_polls_dates",
        "ck_chapters_first_published_date",
        "ck_fanon_links_no_self",
        "ck_followed_users_no_self",
        "ck_group_folders_no_self",
        "ck_stories_published_date",
        "ck_story_arcs_bounds",
        "ck_story_lineages_no_self",
        "ck_users_suspension",
        "ck_vouches_no_self",
    ];

    [Fact]
    public async Task TheCatalogsCheckConstraints_AreExactlyTheDocumentedSet()
    {
        List<string> actual = [];
        await using NpgsqlConnection conn = new(ConnectionString);
        await conn.OpenAsync();
        // conrelid <> 0 leaves out domain checks (information_schema's own); NOT NULL is contype 'n'.
        await using NpgsqlCommand cmd = new("""
            SELECT conname FROM pg_constraint
             WHERE contype = 'c' AND conrelid <> 0 AND connamespace = 'public'::regnamespace
            """, conn);
        await using (NpgsqlDataReader reader = await cmd.ExecuteReaderAsync())
            while (await reader.ReadAsync())
                actual.Add(reader.GetString(0));

        actual.Should().BeEquivalentTo(DocumentedChecks);
    }

    // ── CHECK constraints (23514) ──────────────────────────────────────────────────

    [Fact]
    public async Task StoryArcs_InvertedRange_IsRefused()
    {
        int storyId = await SeedStoryAsync();

        await ExpectViolationAsync(
            $"INSERT INTO story_arcs (story_id, title, start_chapter_number, end_chapter_number) VALUES ({storyId}, 'Backwards', 5, 2)",
            PostgresErrorCodes.CheckViolation, "ck_story_arcs_bounds");
    }

    [Fact]
    public async Task Stories_PublishedStatusWithoutAPublishDate_IsRefused()
    {
        int storyId = await SeedStoryAsync(status: StoryStatusEnum.Completed);

        await ExpectViolationAsync(
            $"UPDATE stories SET published_date = NULL WHERE story_id = {storyId}",
            PostgresErrorCodes.CheckViolation, "ck_stories_published_date");
    }

    [Fact]
    public async Task Stories_UnpublishedStatusesMayKeepOrLackADate()
    {
        // One direction only: a draft has no date, and an unpublished story keeps its original one.
        int draftId = await SeedStoryAsync(status: StoryStatusEnum.Draft);
        int unpublishedId = await SeedStoryAsync(status: StoryStatusEnum.InProgress);

        await ExecuteAsync($"UPDATE stories SET story_status_id = {(short)StoryStatusEnum.Draft} WHERE story_id = {unpublishedId}");
        await ExecuteAsync($"UPDATE stories SET story_status_id = {(short)StoryStatusEnum.Rejected} WHERE story_id = {draftId}");
    }

    [Fact]
    public async Task Chapters_PublishedWithoutAFirstPublishDate_IsRefused()
    {
        int storyId = await SeedStoryAsync();

        await ExpectViolationAsync(
            $"INSERT INTO chapters (story_id, chapter_number, title, is_published, version_count) VALUES ({storyId}, 1, 'Ch', true, 1)",
            PostgresErrorCodes.CheckViolation, "ck_chapters_first_published_date");
        // An unpublished chapter may keep its anchor (D2: never moved).
        await ExecuteAsync(
            $"INSERT INTO chapters (story_id, chapter_number, title, is_published, version_count, first_published_date) VALUES ({storyId}, 2, 'Ch', false, 1, now())");
    }

    [Fact]
    public async Task FollowedUsers_SelfFollow_IsRefused()
    {
        int userId = await SeedUserAsync("SelfFollower");

        await ExpectViolationAsync(
            $"INSERT INTO followed_users (user_id, followed_user_id, receive_alerts) VALUES ({userId}, {userId}, false)",
            PostgresErrorCodes.CheckViolation, "ck_followed_users_no_self");
    }

    [Fact]
    public async Task Vouches_SelfVouch_IsRefused()
    {
        int userId = await SeedUserAsync("SelfVoucher");

        await ExpectViolationAsync(
            $"INSERT INTO vouches (vouching_user_id, vouched_user_id) VALUES ({userId}, {userId})",
            PostgresErrorCodes.CheckViolation, "ck_vouches_no_self");
    }

    [Fact]
    public async Task StoryLineages_SelfLineage_IsRefused()
    {
        int storyId = await SeedStoryAsync();

        await ExpectViolationAsync(
            $"INSERT INTO story_lineages (source_story_id, target_story_id, relationship_type_id, status_id) VALUES ({storyId}, {storyId}, 1, 0)",
            PostgresErrorCodes.CheckViolation, "ck_story_lineages_no_self");
    }

    [Fact]
    public async Task FanonLinks_LinkToItsOwnBaseTag_IsRefused()
    {
        int tagId;
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Tag tag = new() { TagName = $"Species-{Guid.NewGuid():N}"[..20], TagTypeId = TagTypeEnum.Character, AllowCustomName = true };
            db.Tags.Add(tag);
            await db.SaveChangesAsync();
            tagId = tag.TagId;
        }

        await ExpectViolationAsync(
            $"INSERT INTO fanon_links (normalized_name, base_tag_id, target_tag_id) VALUES ('oc', {tagId}, {tagId})",
            PostgresErrorCodes.CheckViolation, "ck_fanon_links_no_self");
    }

    [Fact]
    public async Task GroupFolders_OwnParent_IsRefused()
    {
        (_, int folderId) = await SeedGroupWithFolderAsync("Loop");

        await ExpectViolationAsync(
            $"UPDATE group_folders SET parent_folder_id = group_folder_id WHERE group_folder_id = {folderId}",
            PostgresErrorCodes.CheckViolation, "ck_group_folders_no_self");
    }

    [Fact]
    public async Task BasePolls_ClosingBeforeOpening_IsRefused()
    {
        int ownerId = await SeedUserAsync("PollOwner");

        await ExpectViolationAsync(
            "INSERT INTO base_polls (owner_id, poll_name, date_opened, date_closed, allow_multiple, results_visibility, anonymity_mode) " +
            $"VALUES ({ownerId}, 'Backwards', now(), now() - interval '1 day', false, 0, 0)",
            PostgresErrorCodes.CheckViolation, "ck_base_polls_dates");
    }

    [Fact]
    public async Task Users_SuspensionDateWhileNotSuspended_IsRefused()
    {
        int userId = await SeedUserAsync("Warned");

        await ExpectViolationAsync(
            $"UPDATE \"AspNetUsers\" SET account_status = {(short)AccountStatusEnum.Warned}, suspended_until_utc = now() + interval '1 day' WHERE id = {userId}",
            PostgresErrorCodes.CheckViolation, "ck_users_suspension");
    }

    [Fact]
    public async Task Users_SuspendedWithOrWithoutADate_IsAccepted()
    {
        // One direction only: whether a suspension needs an end date is service §2.1.3(a)'s call.
        int userId = await SeedUserAsync("Suspended");

        await ExecuteAsync(
            $"UPDATE \"AspNetUsers\" SET account_status = {(short)AccountStatusEnum.Suspended}, suspended_until_utc = now() + interval '1 day' WHERE id = {userId}");
        await ExecuteAsync(
            $"UPDATE \"AspNetUsers\" SET suspended_until_utc = NULL WHERE id = {userId}");
    }

    // ── Keys, unique indexes and the folder self-FK ──────────────────────────────────

    [Fact]
    public async Task GroupFolders_DuplicateTopLevelName_IsRefusedByTheNullsNotDistinctIndex()
    {
        (int groupId, _) = await SeedGroupWithFolderAsync("Favorites");

        await ExpectViolationAsync(
            $"INSERT INTO group_folders (group_id, name, max_rating, sort_order) VALUES ({groupId}, 'Favorites', 2, 1)",
            PostgresErrorCodes.UniqueViolation, "ix_group_folders_group_id_parent_folder_id_name");
    }

    [Fact]
    public async Task GroupFolders_NonexistentParent_IsRefusedByTheSelfForeignKey()
    {
        (int groupId, _) = await SeedGroupWithFolderAsync("Real");

        await ExpectViolationAsync(
            $"INSERT INTO group_folders (group_id, parent_folder_id, name, max_rating, sort_order) VALUES ({groupId}, 999999, 'Orphan', 2, 0)",
            PostgresErrorCodes.ForeignKeyViolation, "fk_group_folders_group_folders_parent_folder_id");
    }

    [Fact]
    public async Task GroupFolders_DeletingAParent_SetsItsChildrenToTheTopLevel()
    {
        (int groupId, int parentId) = await SeedGroupWithFolderAsync("Parent");
        await ExecuteAsync(
            $"INSERT INTO group_folders (group_id, parent_folder_id, name, max_rating, sort_order) VALUES ({groupId}, {parentId}, 'Child', 2, 0)");

        await ExecuteAsync($"DELETE FROM group_folders WHERE group_folder_id = {parentId}");

        (await ScalarAsync($"SELECT COUNT(*) FROM group_folders WHERE group_id = {groupId} AND parent_folder_id IS NULL AND name = 'Child'"))
            .Should().Be(1, "ON DELETE SET NULL — before WU-SchemaHardening the child kept a dangling parent id");
    }

    [Fact]
    public async Task GroupStories_SameStoryTwiceInOneGroup_IsRefused()
    {
        (int groupId, _) = await SeedGroupWithFolderAsync("Any");
        int storyId = await SeedStoryAsync();
        await ExecuteAsync($"INSERT INTO group_stories (group_id, story_id, date_added) VALUES ({groupId}, {storyId}, now())");

        await ExpectViolationAsync(
            $"INSERT INTO group_stories (group_id, story_id, date_added) VALUES ({groupId}, {storyId}, now())",
            PostgresErrorCodes.UniqueViolation, "ix_group_stories_group_id_story_id");
    }

    [Fact]
    public async Task FilterSettings_DuplicateTriple_IsRefusedByTheNaturalKey()
    {
        int userId = await SeedUserAsync("Filterer");
        string insert = "INSERT INTO user_story_interaction_filter_settings " +
                        "(user_id, search_mode_key, user_story_interaction_filter_key, is_enabled) " +
                        $"VALUES ({userId}, '{SiteSearchModes.SearchPage}', '{UserStoryInteractionFilters.Completed}', true)";
        await ExecuteAsync(insert);

        await ExpectViolationAsync(insert, PostgresErrorCodes.UniqueViolation, "pk_user_story_interaction_filter_settings");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<(int GroupId, int FolderId)> SeedGroupWithFolderAsync(string folderName)
    {
        int adminId = await SeedUserAsync("GroupAdmin");
        SetActiveUser(adminId);
        using IServiceScope scope = Factory.Services.CreateScope();
        IGroupWriteService groups = scope.ServiceProvider.GetRequiredService<IGroupWriteService>();
        int groupId = await groups.CreateGroupAsync(new CreateGroupDto { GroupName = $"Schema {Guid.NewGuid():N}"[..24] });
        int folderId = await groups.CreateFolderAsync(new CreateFolderDto { GroupId = groupId, Name = folderName });
        return (groupId, folderId);
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

    private async Task<long> ScalarAsync(string sql)
    {
        await using NpgsqlConnection conn = new(ConnectionString);
        await conn.OpenAsync();
        await using NpgsqlCommand cmd = new(sql, conn);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }
}
