using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.SeedTool;

/// <summary>
/// Loads a generated <see cref="SeedGraph"/> via PostgreSQL binary COPY (the AD requirement —
/// EF SaveChanges is orders of magnitude slower at this volume). Column lists mirror the live
/// EF migration snapshot; any schema drift fails LOUDLY at COPY time (column-count/type
/// mismatch), never silently. Insert order respects FK dependencies; the circular
/// chapters↔chapter_contents FK is closed with one post-COPY UPDATE (the same two-step
/// DataSeeder uses). Identity sequences are re-synced with setval afterwards.
/// </summary>
public sealed class SeedBulkWriter(NpgsqlConnection connection)
{
    public async Task WriteAsync(SeedGraph graph, string sharedPasswordHash)
    {
        // Serialized complex-JSON defaults must match EF Core's ToJson shape: PascalCase
        // property names, enums as numbers (both are the System.Text.Json defaults here).
        string readerSettings = JsonSerializer.Serialize(new ReaderSettings());
        string privacySettings = JsonSerializer.Serialize(new PrivacySettings());
        string authorSettings = JsonSerializer.Serialize(new AuthorSettings());

        await CopyUsersAsync(graph.Users, sharedPasswordHash, readerSettings, privacySettings, authorSettings);
        await CopyUserProfilesAsync(graph.Users);
        await CopyUserStatsAsync(graph);
        await CopyStoriesAsync(graph.Stories);
        await CopyStoryListingsAsync(graph.Stories);
        await CopyStoryDetailsAsync(graph.Stories);
        await CopyChaptersAsync(graph.Chapters);
        await CopyChapterContentsAsync(graph.ChapterContents);
        await ExecuteAsync("""
            UPDATE chapters c SET primary_content_id = cc.chapter_content_id
            FROM chapter_contents cc
            WHERE cc.chapter_id = c.chapter_id AND cc.sort_order = 1 AND c.primary_content_id IS NULL
            """);
        await CopyInteractionsAsync(graph.Interactions);
        await CopyInteractionDatesAsync(graph.Interactions);
        await CopyRecommendationsAsync(graph.Recommendations);
        await CopyRecommendationDetailsAsync(graph.Recommendations);
        await CopyVouchesAsync(graph.Vouches);
        await ResyncSequencesAsync();
    }

    private async Task CopyUsersAsync(
        List<SeedUserRow> users, string passwordHash, string readerJson, string privacyJson, string authorJson)
    {
        const string copy = """
            COPY "AspNetUsers" (id, access_failed_count, account_status, active_report_count,
                allow_discovery_from_hidden_favorites, concurrency_stamp, email, email_confirmed,
                lockout_enabled, lockout_end, normalized_email, normalized_user_name, password_hash,
                phone_number, phone_number_confirmed, prefers_animated_sprites, prefers_data_saver_mode,
                profile_picture_relative_url, security_stamp, show_mature_content, suspended_until_utc,
                tagline, theme_id, two_factor_enabled, user_name,
                author_settings, privacy_settings, reader_settings)
            FROM STDIN (FORMAT BINARY)
            """;
        await using NpgsqlBinaryImporter writer = await connection.BeginBinaryImportAsync(copy);
        foreach (SeedUserRow user in users)
        {
            string email = $"{user.UserName}@seed.invalid";
            await writer.StartRowAsync();
            await writer.WriteAsync(user.Id, NpgsqlDbType.Integer);
            await writer.WriteAsync(0, NpgsqlDbType.Integer);                       // access_failed_count
            await writer.WriteAsync((short)AccountStatusEnum.Active, NpgsqlDbType.Smallint);
            await writer.WriteAsync(0, NpgsqlDbType.Integer);                       // active_report_count
            await writer.WriteAsync(user.AllowDiscoveryFromHiddenFavorites, NpgsqlDbType.Boolean);
            await writer.WriteAsync(Guid.NewGuid().ToString(), NpgsqlDbType.Text);  // concurrency_stamp
            await writer.WriteAsync(email, NpgsqlDbType.Varchar);
            await writer.WriteAsync(true, NpgsqlDbType.Boolean);                    // email_confirmed
            await writer.WriteAsync(true, NpgsqlDbType.Boolean);                    // lockout_enabled
            await writer.WriteNullAsync();                                          // lockout_end
            await writer.WriteAsync(email.ToUpperInvariant(), NpgsqlDbType.Varchar);
            await writer.WriteAsync(user.UserName.ToUpperInvariant(), NpgsqlDbType.Varchar);
            await writer.WriteAsync(passwordHash, NpgsqlDbType.Text);
            await writer.WriteNullAsync();                                          // phone_number
            await writer.WriteAsync(false, NpgsqlDbType.Boolean);                   // phone_number_confirmed
            await writer.WriteAsync(true, NpgsqlDbType.Boolean);                    // prefers_animated_sprites
            await writer.WriteAsync(false, NpgsqlDbType.Boolean);                   // prefers_data_saver_mode
            await writer.WriteNullAsync();                                          // profile_picture_relative_url
            await writer.WriteAsync(Guid.NewGuid().ToString("N").ToUpperInvariant(), NpgsqlDbType.Text); // security_stamp
            await writer.WriteAsync(user.ShowMatureContent, NpgsqlDbType.Boolean);
            await writer.WriteNullAsync();                                          // suspended_until_utc
            await writer.WriteNullAsync();                                          // tagline
            await writer.WriteAsync(1, NpgsqlDbType.Integer);                       // theme_id (HasData default theme)
            await writer.WriteAsync(false, NpgsqlDbType.Boolean);                   // two_factor_enabled
            await writer.WriteAsync(user.UserName, NpgsqlDbType.Varchar);
            await writer.WriteAsync(authorJson, NpgsqlDbType.Jsonb);
            await writer.WriteAsync(privacyJson, NpgsqlDbType.Jsonb);
            await writer.WriteAsync(readerJson, NpgsqlDbType.Jsonb);
        }
        await writer.CompleteAsync();
    }

    private async Task CopyUserProfilesAsync(List<SeedUserRow> users)
    {
        await using NpgsqlBinaryImporter writer = await connection.BeginBinaryImportAsync(
            "COPY user_profiles (user_id, text) FROM STDIN (FORMAT BINARY)");
        foreach (SeedUserRow user in users)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(user.Id, NpgsqlDbType.Integer);
            await writer.WriteAsync($"<p>Seed profile text for {user.UserName}.</p>", NpgsqlDbType.Text);
        }
        await writer.CompleteAsync();
    }

    private async Task CopyUserStatsAsync(SeedGraph graph)
    {
        // Denormalized counters kept by construction (the DataSeeder service-bypass contract).
        Dictionary<int, (int Written, long Words)> authored = graph.Stories
            .GroupBy(s => s.AuthorId)
            .ToDictionary(g => g.Key, g => (g.Count(), (long)g.Sum(s => s.WordCount)));
        Dictionary<int, int> recsWritten = graph.Recommendations.Where(r => r.RecommenderId is not null)
            .GroupBy(r => r.RecommenderId!.Value).ToDictionary(g => g.Key, g => g.Count());
        Dictionary<int, int> storyAuthor = graph.Stories.ToDictionary(s => s.Id, s => s.AuthorId);
        Dictionary<int, int> recsReceived = graph.Recommendations
            .GroupBy(r => storyAuthor[r.StoryId]).ToDictionary(g => g.Key, g => g.Count());
        Dictionary<int, int> favsReceived = graph.Interactions.Where(i => i.IsFavorite)
            .GroupBy(i => storyAuthor[i.StoryId]).ToDictionary(g => g.Key, g => g.Count());
        Dictionary<int, (int Read, int InProgress, int Ignored)> reading = graph.Interactions
            .GroupBy(i => i.UserId)
            .ToDictionary(g => g.Key, g => (
                g.Count(i => i.IsCompleted),
                g.Count(i => i.HasStarted && !i.IsCompleted && !i.IsIgnored),
                g.Count(i => i.IsIgnored)));

        const string copy = """
            COPY user_stats (user_id, acknowledged_as_beta_reader_count, acknowledged_as_inspiration_count,
                active_report_count, authors_followed, blog_posts_written, chapters_read, comments_written,
                favorites_on_stories, feature_contributions, follower_count, groups_joined,
                recommendation_successes_earned, recommendations_found_useful, recommendations_received,
                recommendations_written, spotlight_count, stories_ignored, stories_in_progress,
                stories_read, stories_written, views_on_stories, words_read, words_written)
            FROM STDIN (FORMAT BINARY)
            """;
        await using NpgsqlBinaryImporter writer = await connection.BeginBinaryImportAsync(copy);
        foreach (SeedUserRow user in graph.Users)
        {
            (int written, long words) = authored.GetValueOrDefault(user.Id);
            (int read, int inProgress, int ignored) = reading.GetValueOrDefault(user.Id);
            await writer.StartRowAsync();
            await writer.WriteAsync(user.Id, NpgsqlDbType.Integer);
            await writer.WriteAsync(0, NpgsqlDbType.Integer); // acknowledged_as_beta_reader_count
            await writer.WriteAsync(0, NpgsqlDbType.Integer); // acknowledged_as_inspiration_count
            await writer.WriteAsync(0, NpgsqlDbType.Integer); // active_report_count
            await writer.WriteAsync(0, NpgsqlDbType.Integer); // authors_followed
            await writer.WriteAsync(0, NpgsqlDbType.Integer); // blog_posts_written
            await writer.WriteAsync(0, NpgsqlDbType.Integer); // chapters_read
            await writer.WriteAsync(0, NpgsqlDbType.Integer); // comments_written
            await writer.WriteAsync(favsReceived.GetValueOrDefault(user.Id), NpgsqlDbType.Integer);
            await writer.WriteAsync(0, NpgsqlDbType.Integer); // feature_contributions
            await writer.WriteAsync(0, NpgsqlDbType.Integer); // follower_count
            await writer.WriteAsync(0, NpgsqlDbType.Integer); // groups_joined
            await writer.WriteAsync(0, NpgsqlDbType.Integer); // recommendation_successes_earned
            await writer.WriteAsync(0, NpgsqlDbType.Integer); // recommendations_found_useful
            await writer.WriteAsync(recsReceived.GetValueOrDefault(user.Id), NpgsqlDbType.Integer);
            await writer.WriteAsync(recsWritten.GetValueOrDefault(user.Id), NpgsqlDbType.Integer);
            await writer.WriteAsync(0, NpgsqlDbType.Integer); // spotlight_count
            await writer.WriteAsync(ignored, NpgsqlDbType.Integer);
            await writer.WriteAsync(inProgress, NpgsqlDbType.Integer);
            await writer.WriteAsync(read, NpgsqlDbType.Integer);
            await writer.WriteAsync(written, NpgsqlDbType.Integer);
            await writer.WriteAsync(0L, NpgsqlDbType.Bigint); // views_on_stories
            await writer.WriteAsync(0, NpgsqlDbType.Integer); // words_read
            await writer.WriteAsync(words, NpgsqlDbType.Bigint);
        }
        await writer.CompleteAsync();
    }

    private async Task CopyStoriesAsync(List<SeedStoryRow> stories)
    {
        const string copy = """
            COPY stories (story_id, active_report_count, author_id, is_taken_down, last_updated_date,
                original_last_updated_date, original_published_date, published_date, rating,
                story_status_id, takedown_date, takedown_reason, word_count)
            FROM STDIN (FORMAT BINARY)
            """;
        await using NpgsqlBinaryImporter writer = await connection.BeginBinaryImportAsync(copy);
        foreach (SeedStoryRow story in stories)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(story.Id, NpgsqlDbType.Integer);
            await writer.WriteAsync(0, NpgsqlDbType.Integer);
            await writer.WriteAsync(story.AuthorId, NpgsqlDbType.Integer);
            await writer.WriteAsync(false, NpgsqlDbType.Boolean);
            await writer.WriteAsync(story.LastUpdatedUtc, NpgsqlDbType.TimestampTz);
            await writer.WriteNullAsync();
            await writer.WriteNullAsync();
            await writer.WriteAsync(story.PublishedUtc, NpgsqlDbType.TimestampTz);
            await writer.WriteAsync((short)story.Rating, NpgsqlDbType.Smallint);
            await writer.WriteAsync((short)story.Status, NpgsqlDbType.Smallint);
            await writer.WriteNullAsync();
            await writer.WriteNullAsync();
            await writer.WriteAsync(story.WordCount, NpgsqlDbType.Integer);
        }
        await writer.CompleteAsync();
    }

    private async Task CopyStoryListingsAsync(List<SeedStoryRow> stories)
    {
        // search_vector is a stored generated column — deliberately absent from the COPY list.
        await using NpgsqlBinaryImporter writer = await connection.BeginBinaryImportAsync(
            "COPY story_listings (story_id, cover_art_relative_url, short_description, story_title) FROM STDIN (FORMAT BINARY)");
        foreach (SeedStoryRow story in stories)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(story.Id, NpgsqlDbType.Integer);
            await writer.WriteNullAsync();
            await writer.WriteAsync(story.ShortDescription, NpgsqlDbType.Varchar);
            await writer.WriteAsync(story.Title, NpgsqlDbType.Varchar);
        }
        await writer.CompleteAsync();
    }

    private async Task CopyStoryDetailsAsync(List<SeedStoryRow> stories)
    {
        await using NpgsqlBinaryImporter writer = await connection.BeginBinaryImportAsync(
            "COPY story_details (story_id, long_description, post_approval_status, slug) FROM STDIN (FORMAT BINARY)");
        foreach (SeedStoryRow story in stories)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(story.Id, NpgsqlDbType.Integer);
            await writer.WriteAsync($"<p>Seed long description — {story.Title}.</p>", NpgsqlDbType.Text);
            await writer.WriteAsync(
                (short)(story.IsVisible ? story.Status : StoryStatusEnum.InProgress), NpgsqlDbType.Smallint);
            await writer.WriteAsync(story.Slug, NpgsqlDbType.Varchar);
        }
        await writer.CompleteAsync();
    }

    private async Task CopyChaptersAsync(List<SeedChapterRow> chapters)
    {
        // primary_content_id stays NULL here — the circular FK is closed by the UPDATE after
        // chapter_contents lands (same two-step as DataSeeder / the write services).
        await using NpgsqlBinaryImporter writer = await connection.BeginBinaryImportAsync(
            "COPY chapters (chapter_id, chapter_number, is_published, primary_content_id, story_id, title, version_count) FROM STDIN (FORMAT BINARY)");
        foreach (SeedChapterRow chapter in chapters)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(chapter.Id, NpgsqlDbType.Integer);
            await writer.WriteAsync(chapter.Number, NpgsqlDbType.Integer);
            await writer.WriteAsync(chapter.IsPublished, NpgsqlDbType.Boolean);
            await writer.WriteNullAsync();
            await writer.WriteAsync(chapter.StoryId, NpgsqlDbType.Integer);
            await writer.WriteAsync(chapter.Title, NpgsqlDbType.Varchar);
            await writer.WriteAsync(1, NpgsqlDbType.Integer);
        }
        await writer.CompleteAsync();
    }

    private async Task CopyChapterContentsAsync(List<SeedChapterContentRow> contents)
    {
        const string copy = """
            COPY chapter_contents (chapter_content_id, author_id, bottom_authors_note, chapter_id,
                chapter_text, original_publish_date, publish_date, rating, sort_order,
                top_authors_note, version_name, word_count)
            FROM STDIN (FORMAT BINARY)
            """;
        await using NpgsqlBinaryImporter writer = await connection.BeginBinaryImportAsync(copy);
        foreach (SeedChapterContentRow content in contents)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(content.Id, NpgsqlDbType.Bigint);
            await writer.WriteAsync(content.AuthorId, NpgsqlDbType.Integer);
            await writer.WriteNullAsync();
            await writer.WriteAsync(content.ChapterId, NpgsqlDbType.Integer);
            await writer.WriteAsync(content.Html, NpgsqlDbType.Text);
            await writer.WriteNullAsync();
            await writer.WriteAsync(content.PublishUtc, NpgsqlDbType.TimestampTz);
            await writer.WriteNullAsync();                    // rating: inherit the story's
            await writer.WriteAsync(1, NpgsqlDbType.Integer); // sort_order
            await writer.WriteNullAsync();
            await writer.WriteNullAsync();
            await writer.WriteAsync(content.WordCount, NpgsqlDbType.Integer);
        }
        await writer.CompleteAsync();
    }

    private async Task CopyInteractionsAsync(List<SeedInteractionRow> interactions)
    {
        const string copy = """
            COPY user_story_interactions (user_id, story_id, has_started, is_completed, is_favorite,
                is_followed, is_hidden_favorite, is_ignored, is_read_it_later, recommendation_id)
            FROM STDIN (FORMAT BINARY)
            """;
        await using NpgsqlBinaryImporter writer = await connection.BeginBinaryImportAsync(copy);
        foreach (SeedInteractionRow row in interactions)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(row.UserId, NpgsqlDbType.Integer);
            await writer.WriteAsync(row.StoryId, NpgsqlDbType.Integer);
            await writer.WriteAsync(row.HasStarted, NpgsqlDbType.Boolean);
            await writer.WriteAsync(row.IsCompleted, NpgsqlDbType.Boolean);
            await writer.WriteAsync(row.IsFavorite, NpgsqlDbType.Boolean);
            await writer.WriteAsync(row.IsFollowed, NpgsqlDbType.Boolean);
            await writer.WriteAsync(row.IsHiddenFavorite, NpgsqlDbType.Boolean);
            await writer.WriteAsync(row.IsIgnored, NpgsqlDbType.Boolean);
            await writer.WriteAsync(row.IsReadItLater, NpgsqlDbType.Boolean);
            await writer.WriteNullAsync();
        }
        await writer.CompleteAsync();
    }

    private async Task CopyInteractionDatesAsync(List<SeedInteractionRow> interactions)
    {
        const string copy = """
            COPY user_story_interaction_dates (user_id, story_id, completed_date, favorite_date,
                followed_date, hidden_favorite_date, ignored_date, read_it_later_date)
            FROM STDIN (FORMAT BINARY)
            """;
        await using NpgsqlBinaryImporter writer = await connection.BeginBinaryImportAsync(copy);
        foreach (SeedInteractionRow row in interactions)
        {
            if (row.FavoriteDateUtc is null && row.HiddenFavoriteDateUtc is null) continue;
            await writer.StartRowAsync();
            await writer.WriteAsync(row.UserId, NpgsqlDbType.Integer);
            await writer.WriteAsync(row.StoryId, NpgsqlDbType.Integer);
            await writer.WriteNullAsync();
            await WriteNullableAsync(writer, row.FavoriteDateUtc);
            await writer.WriteNullAsync();
            await WriteNullableAsync(writer, row.HiddenFavoriteDateUtc);
            await writer.WriteNullAsync();
            await writer.WriteNullAsync();
        }
        await writer.CompleteAsync();
    }

    private async Task CopyRecommendationsAsync(List<SeedRecommendationRow> recommendations)
    {
        const string copy = """
            COPY recommendations (recommendation_id, active_report_count, date_posted, is_hidden_gem,
                is_highlighted_by_author, is_taken_down, like_count, recommender_id, status_id,
                story_id, successful_rec_count, takedown_date, takedown_reason)
            FROM STDIN (FORMAT BINARY)
            """;
        await using NpgsqlBinaryImporter writer = await connection.BeginBinaryImportAsync(copy);
        foreach (SeedRecommendationRow rec in recommendations)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(rec.Id, NpgsqlDbType.Integer);
            await writer.WriteAsync(0, NpgsqlDbType.Integer);
            await writer.WriteAsync(rec.DatePostedUtc, NpgsqlDbType.TimestampTz);
            await writer.WriteAsync(rec.IsHiddenGem, NpgsqlDbType.Boolean);
            await writer.WriteAsync(rec.IsHighlightedByAuthor, NpgsqlDbType.Boolean);
            await writer.WriteAsync(false, NpgsqlDbType.Boolean);
            await writer.WriteAsync(0, NpgsqlDbType.Integer);
            if (rec.RecommenderId is int recommender) await writer.WriteAsync(recommender, NpgsqlDbType.Integer);
            else await writer.WriteNullAsync();
            await writer.WriteAsync((short)RecommendationStatusEnum.Approved, NpgsqlDbType.Smallint);
            await writer.WriteAsync(rec.StoryId, NpgsqlDbType.Integer);
            await writer.WriteAsync(0, NpgsqlDbType.Integer);
            await writer.WriteNullAsync();
            await writer.WriteNullAsync();
        }
        await writer.CompleteAsync();
    }

    private async Task CopyRecommendationDetailsAsync(List<SeedRecommendationRow> recommendations)
    {
        await using NpgsqlBinaryImporter writer = await connection.BeginBinaryImportAsync(
            "COPY recommendation_details (recommendation_id, text) FROM STDIN (FORMAT BINARY)");
        foreach (SeedRecommendationRow rec in recommendations)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(rec.Id, NpgsqlDbType.Integer);
            await writer.WriteAsync(rec.Text, NpgsqlDbType.Text);
        }
        await writer.CompleteAsync();
    }

    private async Task CopyVouchesAsync(List<SeedVouchRow> vouches)
    {
        await using NpgsqlBinaryImporter writer = await connection.BeginBinaryImportAsync(
            "COPY vouches (vouching_user_id, vouched_user_id, vouch_text, date_vouched) FROM STDIN (FORMAT BINARY)");
        foreach (SeedVouchRow vouch in vouches)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(vouch.VouchingUserId, NpgsqlDbType.Integer);
            await writer.WriteAsync(vouch.VouchedUserId, NpgsqlDbType.Integer);
            await writer.WriteNullAsync();
            await writer.WriteAsync(vouch.DateUtc, NpgsqlDbType.TimestampTz);
        }
        await writer.CompleteAsync();
    }

    private async Task ResyncSequencesAsync()
    {
        // COPY with explicit IDs bypasses the identity sequences — re-sync them so later
        // app-side inserts don't collide.
        await ExecuteAsync("""
            SELECT setval(pg_get_serial_sequence('"AspNetUsers"', 'id'), (SELECT MAX(id) FROM "AspNetUsers"));
            SELECT setval(pg_get_serial_sequence('stories', 'story_id'), (SELECT MAX(story_id) FROM stories));
            SELECT setval(pg_get_serial_sequence('chapters', 'chapter_id'), (SELECT MAX(chapter_id) FROM chapters));
            SELECT setval(pg_get_serial_sequence('chapter_contents', 'chapter_content_id'), (SELECT MAX(chapter_content_id) FROM chapter_contents));
            SELECT setval(pg_get_serial_sequence('recommendations', 'recommendation_id'), (SELECT MAX(recommendation_id) FROM recommendations));
            """);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using NpgsqlCommand command = new(sql, connection) { CommandTimeout = 300 };
        await command.ExecuteNonQueryAsync();
    }

    private static async Task WriteNullableAsync(NpgsqlBinaryImporter writer, DateTime? value)
    {
        if (value is DateTime dt) await writer.WriteAsync(dt, NpgsqlDbType.TimestampTz);
        else await writer.WriteNullAsync();
    }
}
