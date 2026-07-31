using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="UserStatRecalculator"/> (Feature 58, WU-UserStatRecalc). Each
/// test seeds ground-truth rows directly via <see cref="ApplicationDbContext"/>, runs one
/// recalculation pass, and asserts the resulting <c>UserStat</c> counters — never via the
/// real-time <c>ExecuteUpdateAsync</c> write-service path, so a passing test proves the
/// *recompute* formula is correct independent of the increment path.
/// </summary>
[Collection("Postgres")]
public class UserStatRecalculatorTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task RecalculateAllAsync_InsertsMissingUserStatRow()
    {
        int userId = await SeedUserAsync("NoRowYet");

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.UserStats.AnyAsync(s => s.UserId == userId)).Should().BeFalse(
                "SeedUserAsync never creates a UserStat row — no production write path does either");
        }

        UserStatRecalcResult result = await RecalculateAsync();
        result.RowsInserted.Should().BeGreaterThanOrEqualTo(1);

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            UserStat? stat = await db.UserStats.SingleOrDefaultAsync(s => s.UserId == userId);
            stat.Should().NotBeNull("the recalc worker is this user's first UserStat populator");
            stat!.StoriesRead.Should().Be(0);
        }
    }

    [Fact]
    public async Task RecalculateAllAsync_IsIdempotent_SecondPassCorrectsNothing()
    {
        int userId = await SeedUserAsync("Idempotent");
        int storyId = await SeedStoryAsync(userId);
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Stories.SingleAsync(s => s.StoryId == storyId)).WordCount = 500;
            await db.SaveChangesAsync();
        }

        UserStatRecalcResult first = await RecalculateAsync();
        first.RowsInserted.Should().BeGreaterThanOrEqualTo(1);
        first.CountersCorrected.Should().BeGreaterThanOrEqualTo(1, "StoriesWritten/WordsWritten need correcting from 0");

        UserStatRecalcResult second = await RecalculateAsync();
        second.RowsInserted.Should().Be(0);
        second.CountersCorrected.Should().Be(0, "nothing changed between passes — a correct pass is a no-op");
    }

    [Fact]
    public async Task RecalculateAllAsync_CorrectsAuthoredContentCounters()
    {
        int authorId = await SeedUserAsync("Author");
        int storyId = await SeedStoryAsync(authorId);
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Stories.SingleAsync(s => s.StoryId == storyId)).WordCount = 750;

            Chapter chapter = new() { StoryId = storyId, ChapterNumber = 1, Title = "Ch1", IsPublished = true };
            db.Chapters.Add(chapter);
            await db.SaveChangesAsync();

            db.ChapterComments.Add(new ChapterComment
            {
                ChapterId = chapter.ChapterId, UserId = authorId, CommentText = "hi", DatePosted = DateTime.UtcNow,
                IsTakenDown = true, TakedownReason = "test",
            });

            db.ProfileBlogPosts.Add(new ProfileBlogPost
            {
                AuthorId = authorId, Title = "post", Content = "content", IsPublished = true,
                DateCreated = DateTime.UtcNow, LastUpdatedDate = DateTime.UtcNow, Rating = Rating.E,
            });
            await db.SaveChangesAsync();
        }

        await RecalculateAsync();

        using IServiceScope readScope = Factory.Services.CreateScope();
        ApplicationDbContext read = readScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        UserStat stat = await read.UserStats.SingleAsync(s => s.UserId == authorId);
        stat.StoriesWritten.Should().Be(1);
        stat.WordsWritten.Should().Be(750);
        stat.CommentsWritten.Should().Be(1, "takedown doesn't delete the row or exclude it — the wired path doesn't decrement on takedown either");
        stat.BlogPostsWritten.Should().Be(1);
    }

    [Fact]
    public async Task RecalculateAllAsync_CorrectsFollowingCounters()
    {
        int followerId = await SeedUserAsync("Follower");
        int followedId = await SeedUserAsync("Followed");
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.FollowedUsers.Add(new FollowedUser
            {
                UserId = followerId, FollowedUserId = followedId, DateFollowed = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await RecalculateAsync();

        using IServiceScope readScope = Factory.Services.CreateScope();
        ApplicationDbContext read = readScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await read.UserStats.SingleAsync(s => s.UserId == followerId)).AuthorsFollowed.Should().Be(1);
        (await read.UserStats.SingleAsync(s => s.UserId == followedId)).FollowerCount.Should().Be(1);
    }

    [Fact]
    public async Task RecalculateAllAsync_CorrectsGroupsJoined()
    {
        int userId = await SeedUserAsync("Joiner");
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Group group = new()
            {
                CreatorId = userId, GroupName = "G1", AudienceRating = Rating.E,
                MaxContentRating = Rating.E, DateCreated = DateTime.UtcNow,
            };
            db.Groups.Add(group);
            await db.SaveChangesAsync();

            db.GroupMembers.Add(new GroupMember
            {
                UserId = userId, GroupId = group.GroupId, Role = GroupRole.Member, DateJoined = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await RecalculateAsync();

        using IServiceScope readScope = Factory.Services.CreateScope();
        ApplicationDbContext read = readScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await read.UserStats.SingleAsync(s => s.UserId == userId)).GroupsJoined.Should().Be(1);
    }

    [Fact]
    public async Task RecalculateAllAsync_MirrorsWiredFormula_ForInteractionDerivedCounters()
    {
        int readerId = await SeedUserAsync("Reader");
        int authorId = await SeedUserAsync("StoryAuthor");
        int completedStoryId = await SeedStoryAsync(authorId);
        int inProgressIgnoredStoryId = await SeedStoryAsync(authorId);
        int ignoredOnlyStoryId = await SeedStoryAsync(authorId);
        int publicFavoriteStoryId = await SeedStoryAsync(authorId);
        int hiddenFavoriteStoryId = await SeedStoryAsync(authorId);

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // StoriesRead: one completed interaction.
            db.UserStoryInteractions.Add(new UserStoryInteraction
            {
                UserId = readerId, StoryId = completedStoryId, IsCompleted = true,
            });

            // StoriesInProgress: HasStarted && !IsCompleted, EVEN THOUGH IsIgnored is also true —
            // the wired formula (ServerUserStoryInteractionWriteService) does not exclude IsIgnored
            // from this counter (that exclusion is the ActivelyReading *display* filter, a
            // different concept). This interaction must count toward BOTH StoriesInProgress and
            // StoriesIgnored simultaneously.
            db.UserStoryInteractions.Add(new UserStoryInteraction
            {
                UserId = readerId, StoryId = inProgressIgnoredStoryId,
                HasStarted = true, IsCompleted = false, IsIgnored = true,
            });

            // StoriesIgnored only (not started, so not in-progress).
            db.UserStoryInteractions.Add(new UserStoryInteraction
            {
                UserId = readerId, StoryId = ignoredOnlyStoryId, IsIgnored = true,
            });

            // FavoritesOnStories: public IsFavorite counts...
            db.UserStoryInteractions.Add(new UserStoryInteraction
            {
                UserId = readerId, StoryId = publicFavoriteStoryId, IsFavorite = true,
            });
            // ...IsHiddenFavorite alone must NOT count.
            db.UserStoryInteractions.Add(new UserStoryInteraction
            {
                UserId = readerId, StoryId = hiddenFavoriteStoryId, IsHiddenFavorite = true,
            });

            await db.SaveChangesAsync();
        }

        await RecalculateAsync();

        using IServiceScope readScope = Factory.Services.CreateScope();
        ApplicationDbContext read = readScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        UserStat readerStat = await read.UserStats.SingleAsync(s => s.UserId == readerId);
        readerStat.StoriesRead.Should().Be(1);
        readerStat.StoriesInProgress.Should().Be(1, "HasStarted && !IsCompleted, regardless of IsIgnored");
        readerStat.StoriesIgnored.Should().Be(2, "both the in-progress-and-ignored row and the ignored-only row");

        UserStat authorStat = await read.UserStats.SingleAsync(s => s.UserId == authorId);
        authorStat.FavoritesOnStories.Should().Be(1, "only the public IsFavorite row — never IsHiddenFavorite");
    }

    [Fact]
    public async Task RecalculateAllAsync_CorrectsRecommendationCounters_WithAntiSelfFarmExclusion()
    {
        int authorId = await SeedUserAsync("RecAuthor");
        int recommenderId = await SeedUserAsync("Recommender");
        int readerId = await SeedUserAsync("HelpfulReader");
        int storyId = await SeedStoryAsync(authorId);
        // A distinct second story for the self-farm recommendation — (recommender_id, story_id)
        // is uniquely constrained, so the same recommender can't recommend the same story twice.
        int secondStoryId = await SeedStoryAsync(authorId);

        int recommendationId;
        int selfRecommendationId;
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Recommendation rec = new()
            {
                StoryId = storyId, RecommenderId = recommenderId, StatusId = 2, DatePosted = DateTime.UtcNow,
                RecommendationDetail = new RecommendationDetail { Text = "great story" },
            };
            db.Recommendations.Add(rec);

            // A recommendation whose recommender is ALSO the one confirming success — must be
            // excluded from RecommendationSuccessesEarned by the anti-self-farm rule.
            Recommendation selfRec = new()
            {
                StoryId = secondStoryId, RecommenderId = recommenderId, StatusId = 2, DatePosted = DateTime.UtcNow,
                RecommendationDetail = new RecommendationDetail { Text = "self farm attempt" },
            };
            db.Recommendations.Add(selfRec);
            await db.SaveChangesAsync();
            recommendationId = rec.RecommendationId;
            selfRecommendationId = selfRec.RecommendationId;

            db.RecommendationSuccesses.Add(new RecommendationSuccess
            {
                UserId = readerId, RecommendationId = recommendationId, DateRecorded = DateTime.UtcNow,
            });
            db.RecommendationSuccesses.Add(new RecommendationSuccess
            {
                UserId = recommenderId, RecommendationId = selfRecommendationId, DateRecorded = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await RecalculateAsync();

        using IServiceScope readScope = Factory.Services.CreateScope();
        ApplicationDbContext read = readScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await read.UserStats.SingleAsync(s => s.UserId == recommenderId)).RecommendationsWritten.Should().Be(2);
        (await read.UserStats.SingleAsync(s => s.UserId == authorId)).RecommendationsReceived.Should().Be(2);
        (await read.UserStats.SingleAsync(s => s.UserId == recommenderId)).RecommendationSuccessesEarned.Should().Be(
            1, "the self-recommendation success is excluded by the anti-self-farm join");
        (await read.UserStats.SingleAsync(s => s.UserId == readerId)).RecommendationsFoundUseful.Should().Be(
            1, "reader-side counter — every recorded success counts, no anti-self-farm filter");
        (await read.UserStats.SingleAsync(s => s.UserId == recommenderId)).RecommendationsFoundUseful.Should().Be(
            1, "the recommender ALSO recorded a success (on their own rec) from the reader-side perspective");
    }

    [Fact]
    public async Task RecalculateAllAsync_CorrectsReadingCounters_ChaptersReadAndWordsRead()
    {
        int authorId = await SeedUserAsync("ChapterAuthor");
        int readerId = await SeedUserAsync("ChapterReader");
        int storyId = await SeedStoryAsync(authorId);

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Chapter chapter = new() { StoryId = storyId, ChapterNumber = 1, Title = "Ch1", IsPublished = true };
            db.Chapters.Add(chapter);
            await db.SaveChangesAsync();

            ChapterContent content = new()
            {
                ChapterId = chapter.ChapterId, AuthorId = authorId, ChapterText = "text",
                WordCount = 1200, PublishDate = DateTime.UtcNow,
            };
            db.ChapterContents.Add(content);
            await db.SaveChangesAsync();

            chapter.PrimaryContentId = content.ChapterContentId;
            await db.SaveChangesAsync();

            db.UserChapterInteractions.Add(new UserChapterInteraction
            {
                UserId = readerId, ChapterId = chapter.ChapterId, IsRead = true, LastInteractionDate = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await RecalculateAsync();

        using IServiceScope readScope = Factory.Services.CreateScope();
        ApplicationDbContext read = readScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        UserStat readerStat = await read.UserStats.SingleAsync(s => s.UserId == readerId);
        readerStat.ChaptersRead.Should().Be(1);
        readerStat.WordsRead.Should().Be(1200);
    }

    [Fact]
    public async Task RecalculateAllAsync_CorrectsViewsOnStories_ViaRawSqlMart()
    {
        int authorId = await SeedUserAsync("ViewedAuthor");
        int storyId = await SeedStoryAsync(authorId);

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            DateOnly day1 = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2));
            DateOnly day2 = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO daily_story_stats (story_id, stat_date, view_count) VALUES ({storyId}, {day1}, 40)");
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO daily_story_stats (story_id, stat_date, view_count) VALUES ({storyId}, {day2}, 17)");
        }

        await RecalculateAsync();

        using IServiceScope readScope = Factory.Services.CreateScope();
        ApplicationDbContext read = readScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await read.UserStats.SingleAsync(s => s.UserId == authorId)).ViewsOnStories.Should().Be(
            57, "lifetime total = SUM over the story's daily_story_stats rows");
    }

    [Fact]
    public async Task RecalculateAllAsync_ZeroesDriftedCounterWithNoGroundTruth()
    {
        int userId = await SeedUserAsync("Drifted");
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // Simulate drift: a wrong positive value with zero backing ground-truth rows (e.g. a
            // bug or manual DB edit left a stale value after every interaction was removed).
            db.UserStats.Add(new UserStat { UserId = userId, StoriesRead = 5, FollowerCount = 3 });
            await db.SaveChangesAsync();
        }

        UserStatRecalcResult result = await RecalculateAsync();
        result.CountersCorrected.Should().BeGreaterThanOrEqualTo(2);

        using IServiceScope readScope = Factory.Services.CreateScope();
        ApplicationDbContext read = readScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        UserStat stat = await read.UserStats.SingleAsync(s => s.UserId == userId);
        stat.StoriesRead.Should().Be(0, "a plain inner join against the aggregate would silently skip this user");
        stat.FollowerCount.Should().Be(0);
    }

    [Fact]
    public async Task RecalculateAllAsync_LeavesSpotlightCountUntouched()
    {
        // Only SpotlightCount remains deferred (WU-StatBadgeProducers re-filed it under tracker B8)
        // — AcknowledgedAsBetaReaderCount/AcknowledgedAsInspirationCount are now recomputed, covered
        // by the tests below.
        int userId = await SeedUserAsync("Deferred");
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.UserStats.Add(new UserStat { UserId = userId, SpotlightCount = 7 });
            await db.SaveChangesAsync();
        }

        await RecalculateAsync();

        using IServiceScope readScope = Factory.Services.CreateScope();
        ApplicationDbContext read = readScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await read.UserStats.SingleAsync(s => s.UserId == userId)).SpotlightCount.Should().Be(
            7, "producer deferred to tracker B8 — recomputing to 0 would mask that, not correct drift");
    }

    [Fact]
    public async Task RecalculateAllAsync_CorrectsAcknowledgedAsBetaReaderCount_AcceptedRoleOneOnly()
    {
        int authorId = await SeedUserAsync("CreditingAuthor");
        int recipientId = await SeedUserAsync("Recipient");
        int storyId = await SeedStoryAsync(authorId);
        int secondStoryId = await SeedStoryAsync(authorId);
        int thirdStoryId = await SeedStoryAsync(authorId);

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // Counts: Accepted, role 1 (Beta Reader).
            db.StoryAcknowledgments.Add(new StoryAcknowledgment
            {
                StoryId = storyId, AcknowledgedUserId = recipientId, AcknowledgmentRoleId = 1,
                StatusId = StoryAcknowledgmentStatus.Accepted,
            });
            // Does NOT count: still Pending.
            db.StoryAcknowledgments.Add(new StoryAcknowledgment
            {
                StoryId = secondStoryId, AcknowledgedUserId = recipientId, AcknowledgmentRoleId = 1,
                StatusId = StoryAcknowledgmentStatus.Pending,
            });
            // Does NOT count: Accepted, but role 2 (Planner) — not Beta Reader.
            db.StoryAcknowledgments.Add(new StoryAcknowledgment
            {
                StoryId = thirdStoryId, AcknowledgedUserId = recipientId, AcknowledgmentRoleId = 2,
                StatusId = StoryAcknowledgmentStatus.Accepted,
            });
            await db.SaveChangesAsync();
        }

        await RecalculateAsync();

        using IServiceScope readScope = Factory.Services.CreateScope();
        ApplicationDbContext read = readScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await read.UserStats.SingleAsync(s => s.UserId == recipientId)).AcknowledgedAsBetaReaderCount
            .Should().Be(1, "only the Accepted, role-1 (Beta Reader) row counts");
    }

    [Fact]
    public async Task RecalculateAllAsync_CorrectsAcknowledgedAsInspirationCount_ApprovedInspiredByCrossAuthorOnly()
    {
        int inspiringAuthorId = await SeedUserAsync("InspiringAuthor");
        int otherAuthorId = await SeedUserAsync("OtherAuthor");
        int sourceStoryId = await SeedStoryAsync(otherAuthorId);
        int targetStoryId = await SeedStoryAsync(inspiringAuthorId);
        int pendingSourceId = await SeedStoryAsync(otherAuthorId);
        int pendingTargetId = await SeedStoryAsync(inspiringAuthorId);
        int selfSourceId = await SeedStoryAsync(inspiringAuthorId);
        int selfTargetId = await SeedStoryAsync(inspiringAuthorId);

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // Counts: Approved, type 1 (Inspired By), cross-author — target author was inspired BY
            // the source, so the counter belongs to the TARGET author.
            db.StoryLineages.Add(new StoryLineage
            {
                SourceStoryId = sourceStoryId, TargetStoryId = targetStoryId, RelationshipTypeId = 1,
                StatusId = StoryLineageStatus.Approved,
            });
            // Does NOT count: still Pending.
            db.StoryLineages.Add(new StoryLineage
            {
                SourceStoryId = pendingSourceId, TargetStoryId = pendingTargetId, RelationshipTypeId = 1,
                StatusId = StoryLineageStatus.Pending,
            });
            // Does NOT count: Approved and type 1, but same author on both sides (self-owned,
            // auto-approved by RequestLineageAsync) — not a real inspiration credit.
            db.StoryLineages.Add(new StoryLineage
            {
                SourceStoryId = selfSourceId, TargetStoryId = selfTargetId, RelationshipTypeId = 1,
                StatusId = StoryLineageStatus.Approved,
            });
            await db.SaveChangesAsync();
        }

        await RecalculateAsync();

        using IServiceScope readScope = Factory.Services.CreateScope();
        ApplicationDbContext read = readScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await read.UserStats.SingleAsync(s => s.UserId == inspiringAuthorId)).AcknowledgedAsInspirationCount
            .Should().Be(1, "only the Approved, type-1 (Inspired By) cross-author link counts");
    }

    [Fact]
    public async Task RecalculateAllAsync_SyncsUserBadgeEarnedCount_FromCorrectedUserStatColumn()
    {
        int recommenderId = await SeedUserAsync("BadgeHolder");
        int readerId = await SeedUserAsync("HelpfulReader2");
        int authorId = await SeedUserAsync("RecAuthor2");
        int storyId = await SeedStoryAsync(authorId);

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Recommendation rec = new()
            {
                StoryId = storyId, RecommenderId = recommenderId, StatusId = 2, DatePosted = DateTime.UtcNow,
                RecommendationDetail = new RecommendationDetail { Text = "great story" },
            };
            db.Recommendations.Add(rec);
            await db.SaveChangesAsync();

            db.RecommendationSuccesses.Add(new RecommendationSuccess
            {
                UserId = readerId, RecommendationId = rec.RecommendationId, DateRecorded = DateTime.UtcNow,
            });
            // A drifted UserBadge row: earned, but EarnedCount stuck at a stale value.
            db.UserBadges.Add(new UserBadge
            {
                UserId = recommenderId, BadgeKey = SiteBadges.Recommender, DisplayOrder = 1, EarnedCount = 99,
            });
            await db.SaveChangesAsync();
        }

        UserStatRecalcResult result = await RecalculateAsync();
        result.CountersCorrected.Should().BeGreaterThanOrEqualTo(2, "both the UserStat column and the badge EarnedCount drift-correct");

        using IServiceScope readScope = Factory.Services.CreateScope();
        ApplicationDbContext read = readScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await read.UserStats.SingleAsync(s => s.UserId == recommenderId)).RecommendationSuccessesEarned.Should().Be(1);
        UserBadge badge = await read.UserBadges.SingleAsync(ub => ub.UserId == recommenderId && ub.BadgeKey == SiteBadges.Recommender);
        badge.EarnedCount.Should().Be(1, "EarnedCount must sync to the now-corrected UserStat column, not stay at the drifted value");
    }

    [Fact]
    public async Task RecalculateAllAsync_DoesNotAwardMissingBadges()
    {
        // A user whose counter would justify the badge, but who was never actually awarded it
        // (e.g. a broken producer) — recalc must NOT mint the badge. Awarding stays the
        // producers' job; a recalc that mints badges would hide a broken producer instead of
        // surfacing it (see UserStatRecalculator's class doc).
        int recommenderId = await SeedUserAsync("NeverAwarded");
        int readerId = await SeedUserAsync("HelpfulReader3");
        int authorId = await SeedUserAsync("RecAuthor3");
        int storyId = await SeedStoryAsync(authorId);

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Recommendation rec = new()
            {
                StoryId = storyId, RecommenderId = recommenderId, StatusId = 2, DatePosted = DateTime.UtcNow,
                RecommendationDetail = new RecommendationDetail { Text = "great story" },
            };
            db.Recommendations.Add(rec);
            await db.SaveChangesAsync();

            db.RecommendationSuccesses.Add(new RecommendationSuccess
            {
                UserId = readerId, RecommendationId = rec.RecommendationId, DateRecorded = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            // Deliberately no UserBadge row inserted.
        }

        await RecalculateAsync();

        using IServiceScope readScope = Factory.Services.CreateScope();
        ApplicationDbContext read = readScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await read.UserStats.SingleAsync(s => s.UserId == recommenderId)).RecommendationSuccessesEarned.Should().Be(1);
        (await read.UserBadges.AnyAsync(ub => ub.UserId == recommenderId && ub.BadgeKey == SiteBadges.Recommender))
            .Should().BeFalse("recalc corrects counters, it never awards — that would mask a broken producer");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────

    private async Task<UserStatRecalcResult> RecalculateAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        UserStatRecalculator recalculator = scope.ServiceProvider.GetRequiredService<UserStatRecalculator>();
        return await recalculator.RecalculateAllAsync();
    }
}
