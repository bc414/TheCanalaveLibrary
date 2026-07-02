using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Development-only seeder. Runs on every Development startup (Program.cs), applies migrations
/// (creating the database when missing), and — when the database is empty — populates a
/// deterministic representative dataset so every major UI surface has something to show.
///
/// <para><b>Mode</b> (config key <c>DevSeed</c>, read lazily at run time):
/// <c>Full</c> (default; the showcase below), <c>Minimal</c> (ONLY <c>TestUser</c> +
/// <c>AdminUser</c> + roles — pinned by <c>TestAppFactory</c>, because this method runs before
/// every integration test and each extra user costs a slow PBKDF2 password hash per test),
/// <c>None</c> (migrate only).</para>
///
/// <para><b>Guard:</b> seeding is skipped entirely when a user named <c>TestUser</c> exists.
/// With the wipe workflow this means the seeder only ever writes into an empty database —
/// to change seed content, edit this file and run <c>scripts/reset-dev-db.ps1 -Restart</c>
/// (see <c>run-server/SKILL.md</c> "Dev DB lifecycle"). Do not add incremental top-up logic.</para>
///
/// <para><b>Deliberate service bypass:</b> rows are inserted through raw
/// <see cref="ApplicationDbContext"/> graphs, not the write services — services are self-scoped
/// via <c>IActiveUserContext</c> (no authenticated user exists at startup). Denormalized
/// invariants the services normally maintain are therefore kept by construction here: word
/// counts via <see cref="ChapterText.CountWords"/>, <c>LikeCount</c>s matching inserted like
/// rows, <c>Chapter.PrimaryContentId</c>/<c>VersionCount</c>, <c>UserStat</c> counters matching
/// the content. All HTML is hand-written within the sanitizer allow-list (p/strong/em only).</para>
///
/// <para><b>Naming:</b> deliberately artificial ("AuthorAlpha", "Seed chapter body…") — seed data
/// must never masquerade as community-authored content. Tag names are real franchise taxonomy
/// (site vocabulary, mod-curated in production), which is the one sanctioned exception.</para>
///
/// <para><b>Full-mode inventory</b> (ids are deterministic on a fresh database; TestUser=1,
/// AdminUser=2):
/// 7 users (TestUser everyman · AdminUser Admin+Moderator · ModUser Moderator · AuthorAlpha ·
/// AuthorBeta mature author · ReaderGamma mature-off · LurkerDelta private profile);
/// 44 tags (20 characters, 8 settings, 12 genres, 4 content warnings);
/// 12 stories across ratings E/T/M and statuses (incl. 2 PendingApproval for the mod queue,
/// 1 Draft), one 5-chapter story with an alternate chapter version, one 2-chapter + unpublished
/// draft; follows/vouches; TestUser bookshelf rows covering every tab; chapter comments with a
/// reply thread + spoiler + likes, a profile comment, a group comment; 3 approved recommendations
/// (one Hidden Gem, one author-highlighted) with likes; 3 groups (standard w/ folders + stories +
/// blog post, SFW-only, Mature); 2 profile blog posts (published w/ linked story + draft);
/// 1 conversation with an unread message for TestUser; 3 notifications for TestUser;
/// 2 open reports (story + comment).</para>
/// </summary>
public class DataSeeder(
    ApplicationDbContext context,
    UserManager<User> userManager,
    IConfiguration configuration)
{
    private static readonly DateTime Now = DateTime.UtcNow;

    public async Task SeedDevelopmentDataAsync()
    {
        // Applies pending migrations; also CREATEs the database when it doesn't exist —
        // which is what makes scripts/reset-dev-db.ps1 a plain DROP with no CREATE step.
        await context.Database.MigrateAsync();

        string mode = configuration["DevSeed"] ?? "Full";
        if (string.Equals(mode, "None", StringComparison.OrdinalIgnoreCase)) return;

        if (await context.Users.AnyAsync(u => u.UserName == "TestUser"))
            return; // already seeded — wipe the DB (scripts/reset-dev-db.ps1) to re-seed

        bool full = string.Equals(mode, "Full", StringComparison.OrdinalIgnoreCase);

        // Minimal seeds ONLY the dev-bar pair: Identity password hashing is deliberately slow,
        // and under TestAppFactory this method runs before every integration test — each extra
        // user costs a PBKDF2 hash per test across the whole suite.
        var users = await SeedUsersAsync(full);

        if (!full)
            return; // Minimal: TestUser + AdminUser + roles only (tests seed their own data)

        var tags = await SeedTagsAsync();
        var stories = await SeedStoriesAsync(users, tags);
        await SeedChaptersAsync(users, stories);
        await SeedSocialGraphAsync(users, stories);
        await SeedCommentsAndRecommendationsAsync(users, stories);
        await SeedGroupsAsync(users, stories);
        await SeedBlogPostsAsync(users, stories);
        await SeedMessagingAsync(users);
        await SeedNotificationsAndReportsAsync(users, stories);
        await SeedProfilesAndStatsAsync(users);
    }

    // ── Users ────────────────────────────────────────────────────────────────────

    private sealed record SeedUsers(
        User Test, User Admin, User Mod,
        User AuthorAlpha, User AuthorBeta, User ReaderGamma, User LurkerDelta)
    {
        /// <summary>Minimal-mode shape: only the dev-bar pair exists; the rest are null.</summary>
        public static SeedUsers MinimalPair(User test, User admin) =>
            new(test, admin, null!, null!, null!, null!, null!);
    }

    private async Task<SeedUsers> SeedUsersAsync(bool full)
    {
        async Task<User> CreateAsync(string name, string tagline, Action<User>? mutate = null)
        {
            var u = new User
            {
                UserName = name,
                Email = $"{name.ToLowerInvariant()}@example.com",
                EmailConfirmed = true,
                ThemeId = 1, // "Pokémon" theme, seeded via HasData
                Tagline = tagline,
            };
            mutate?.Invoke(u);
            IdentityResult result = await userManager.CreateAsync(u, "Password123!");
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"DataSeeder: creating {name} failed: {string.Join("; ", result.Errors.Select(e => e.Description))}");
            return u;
        }

        // Order matters for deterministic ids: TestUser=1, AdminUser=2 (dev-bar contract).
        User test = await CreateAsync("TestUser", "Seed account: the everyman used by the dev login bar.",
            u => u.ShowMatureContent = true);
        User admin = await CreateAsync("AdminUser", "Seed account: Admin + Moderator roles.",
            u => u.ShowMatureContent = true);

        await userManager.AddToRoleAsync(admin, "Admin");
        await userManager.AddToRoleAsync(admin, "Moderator");

        if (!full)
            return SeedUsers.MinimalPair(test, admin);

        User mod = await CreateAsync("ModUser", "Seed account: Moderator role only.");
        User alpha = await CreateAsync("AuthorAlpha", "Seed account: prolific author (multi-chapter stories, blog).");
        User beta = await CreateAsync("AuthorBeta", "Seed account: mature-content author.",
            u => u.ShowMatureContent = true);
        User gamma = await CreateAsync("ReaderGamma", "Seed account: reader with mature content OFF.");
        User delta = await CreateAsync("LurkerDelta", "Seed account: private profile settings.",
            u => u.PrivacySettings = new PrivacySettings { ProfileVisibility = ProfileVisibility.Private });

        await userManager.AddToRoleAsync(mod, "Moderator");

        return new SeedUsers(test, admin, mod, alpha, beta, gamma, delta);
    }

    // ── Tags ─────────────────────────────────────────────────────────────────────

    private sealed record SeedTags(
        List<Tag> Characters, List<Tag> Settings, List<Tag> Genres, List<Tag> Warnings);

    private async Task<SeedTags> SeedTagsAsync()
    {
        // Real franchise taxonomy (site vocabulary), not community content. Sprites stay null —
        // the sprite pipeline resolves keys only when present.
        List<Tag> characters = new[]
            {
                "Cynthia", "Lucas", "Dawn", "Barry", "Professor Rowan", "Cyrus", "Looker",
                "Riley", "Byron", "Roark", "Gardenia", "Fantina", "Maylene", "Candice",
                "Volkner", "Flint", "Bertha", "Aaron", "Lucian", "Mars",
            }
            .Select(n => new Tag { TagName = n, TagTypeId = TagTypeEnum.Character })
            .ToList();
        // One species tag with a sprite key matching the checked-in dev asset
        // (wwwroot/sprites/themes/pokemon/static/bulbasaur.png) so the optimistic sprite-URL
        // render path is exercisable on a fresh database.
        characters.Add(new Tag
        {
            TagName = "Bulbasaur",
            TagTypeId = TagTypeEnum.Character,
            SpriteIdentifier = "bulbasaur",
        });
        // Structured-tag gate exercise: one character allows OC details.
        characters[0].AllowOCDetails = true;

        List<Tag> settings = new[]
            {
                "Sinnoh", "Canalave City", "Jubilife City", "Mt. Coronet", "Eterna Forest",
                "Sunyshore City", "Distortion World", "Iron Island",
            }
            .Select(n => new Tag { TagName = n, TagTypeId = TagTypeEnum.Setting })
            .ToList();
        settings[0].AllowSettingDetails = true;

        List<Tag> genres = new[]
            {
                "Adventure", "Romance", "Mystery", "Comedy", "Drama", "Slice of Life",
                "Action", "Friendship", "Tragedy", "Horror", "Alternate Universe", "Character Study",
            }
            .Select(n => new Tag { TagName = n, TagTypeId = TagTypeEnum.Genre })
            .ToList();

        List<Tag> warnings = new[]
            {
                "Violence", "Character Death", "Heavy Themes", "Spoilers: Platinum",
            }
            .Select(n => new Tag { TagName = n, TagTypeId = TagTypeEnum.ContentWarning })
            .ToList();

        context.Tags.AddRange(characters);
        context.Tags.AddRange(settings);
        context.Tags.AddRange(genres);
        context.Tags.AddRange(warnings);
        await context.SaveChangesAsync();

        return new SeedTags(characters, settings, genres, warnings);
    }

    // ── Stories ──────────────────────────────────────────────────────────────────

    private async Task<List<Story>> SeedStoriesAsync(SeedUsers users, SeedTags tags)
    {
        var stories = new List<Story>();
        int slugIndex = 0;

        Story Make(User author, string title, Rating rating, StoryStatusEnum status,
            int daysOld, string shortDesc, params Tag[] storyTags)
        {
            var story = new Story
            {
                Author = author,
                Rating = rating,
                StoryStatusId = status,
                WordCount = 0, // real value set when chapters land (SeedChaptersAsync)
                ViewCount = 25 + 13 * slugIndex,
                PublishedDate = Now.AddDays(-60 + daysOld),
                LastUpdatedDate = Now.AddDays(-10 + daysOld % 10),
                StoryListing = new StoryListing { StoryTitle = title, ShortDescription = shortDesc },
                StoryDetail = new StoryDetail
                {
                    LongDescription = $"<p>Seed long description for “{title}”. Status: {status}, rating: {rating}.</p>",
                    Slug = $"seed-story-{++slugIndex}",
                    // ApproveStoryAsync transitions the story TO this value, so a PendingApproval
                    // story must carry its intended published status here — PendingApproval itself
                    // would make moderator approval a silent no-op (found in the L4.5 browser pass).
                    PostApprovalStatus = status == StoryStatusEnum.PendingApproval
                        ? StoryStatusEnum.InProgress
                        : status,
                },
            };
            foreach (Tag t in storyTags)
                story.StoryTags.Add(new StoryTag { Tag = t, Priority = TagPriority.Primary });
            stories.Add(story);
            return story;
        }

        var c = tags.Characters;
        var s = tags.Settings;
        var g = tags.Genres;
        var w = tags.Warnings;

        // 1 — the flagship: 5 chapters, one alternate version (see SeedChaptersAsync).
        Story flagship = Make(users.AuthorAlpha, "Seed Story: Five Chapters + Alt Version (T)",
            Rating.T, StoryStatusEnum.InProgress, 5,
            "Seed story exercising multi-chapter reading and chapter versioning.", g[0], s[0]);
        flagship.StoryCharacters.Add(new StoryCharacter { CharacterTag = c[0], Priority = TagPriority.Primary });
        flagship.StoryCharacters.Add(new StoryCharacter { CharacterTag = c[1], Priority = TagPriority.Supporting });

        // 2 — two published chapters plus an unpublished draft chapter.
        Story draftChapter = Make(users.AuthorAlpha, "Seed Story: Draft Third Chapter (E)",
            Rating.E, StoryStatusEnum.InProgress, 8,
            "Seed story with an unpublished draft chapter.", g[1], s[1]);
        draftChapter.StoryCharacters.Add(new StoryCharacter { CharacterTag = c[2], Priority = TagPriority.Primary });

        // 3 — completed single-chapter.
        Make(users.AuthorAlpha, "Seed Story: Completed One-Shot (E)",
            Rating.E, StoryStatusEnum.Completed, 12,
            "Seed one-shot; Completed status.", g[5], s[2]);

        // 4 — mature story by the mature author (invisible to ReaderGamma).
        Make(users.AuthorBeta, "Seed Story: Mature (M)",
            Rating.M, StoryStatusEnum.InProgress, 15,
            "Seed mature-rated story; hidden from mature-off viewers.", g[4], s[6], w[2]);

        // 5 — on hiatus.
        Make(users.AuthorBeta, "Seed Story: On Hiatus (T)",
            Rating.T, StoryStatusEnum.OnHiatus, 20,
            "Seed story with OnHiatus status.", g[2], s[3]);

        // 6 — OC details (character tag 0 allows them).
        Story oc = Make(users.AuthorBeta, "Seed Story: With OC Details (T)",
            Rating.T, StoryStatusEnum.InProgress, 24,
            "Seed story carrying an original-character entry.", g[7], s[4]);
        oc.StoryCharacters.Add(new StoryCharacter
        {
            CharacterTag = c[0],
            Priority = TagPriority.Primary,
            IsOc = true,
            OcName = "Seed OC Name",
            OcBio = "Seed OC bio text (plain).",
        });

        // 7 — character pairing.
        Story pairing = Make(users.AuthorAlpha, "Seed Story: Character Pairing (T)",
            Rating.T, StoryStatusEnum.InProgress, 28,
            "Seed story with a two-character pairing.", g[1], s[5]);
        var pc1 = new StoryCharacter { CharacterTag = c[3], Priority = TagPriority.Primary };
        var pc2 = new StoryCharacter { CharacterTag = c[4], Priority = TagPriority.Primary };
        pairing.StoryCharacters.Add(pc1);
        pairing.StoryCharacters.Add(pc2);
        pairing.StoryCharacterPairings.Add(new StoryCharacterPairing
        {
            PairingType = CharacterPairingType.Romantic,
            Priority = TagPriority.Primary,
            Members =
            {
                new StoryCharacterPairingMember { StoryCharacter = pc1 },
                new StoryCharacterPairingMember { StoryCharacter = pc2 },
            },
        });

        // 8/9 — pending approval ×2 (mod submissions queue).
        Make(users.Test, "Seed Story: Pending Approval A (E)",
            Rating.E, StoryStatusEnum.PendingApproval, 30,
            "Seed story awaiting moderator approval.", g[6], s[0]);
        Make(users.AuthorBeta, "Seed Story: Pending Approval B (M)",
            Rating.M, StoryStatusEnum.PendingApproval, 32,
            "Seed mature story awaiting moderator approval.", g[8], s[6], w[0]);

        // 10 — draft (unlisted-by-status surface).
        Make(users.AuthorAlpha, "Seed Story: Draft (E)",
            Rating.E, StoryStatusEnum.Draft, 34,
            "Seed story in Draft status.", g[10], s[7]);

        // 11/12 — filler for listings/pagination variety.
        Make(users.Test, "Seed Story: TestUser's Own (E)",
            Rating.E, StoryStatusEnum.InProgress, 38,
            "Seed story authored by TestUser (bookshelves 'My Stories').", g[0], s[1]);
        Make(users.AuthorBeta, "Seed Story: Filler (T)",
            Rating.T, StoryStatusEnum.Completed, 42,
            "Seed completed story for listing variety.", g[3], s[2], w[3]);

        context.Stories.AddRange(stories);
        await context.SaveChangesAsync();
        return stories;
    }

    // ── Chapters ─────────────────────────────────────────────────────────────────

    private async Task SeedChaptersAsync(SeedUsers users, List<Story> stories)
    {
        static string Body(string title, int paragraphs)
        {
            var parts = Enumerable.Range(1, paragraphs)
                .Select(i => $"<p>Seed chapter body for “{title}”, paragraph {i}. " +
                             "Plain filler text — deliberately not story prose.</p>");
            return string.Join("", parts);
        }

        var fixups = new List<(Chapter Chapter, ChapterContent Primary)>();

        Chapter AddChapter(Story story, int number, string title, bool published, int paragraphs)
        {
            string html = Body(title, paragraphs);
            var content = new ChapterContent
            {
                AuthorId = story.AuthorId,
                ChapterText = html,
                WordCount = ChapterText.CountWords(html),
                SortOrder = 1,
                Rating = null, // inherit story rating (primary invariant)
                PublishDate = Now.AddDays(-40 + number),
            };
            var chapter = new Chapter
            {
                Story = story,
                ChapterNumber = number,
                Title = title,
                IsPublished = published,
                VersionCount = 1,
                ChapterContents = { content },
            };
            context.Chapters.Add(chapter);
            fixups.Add((chapter, content));
            if (published) story.WordCount += content.WordCount;
            return chapter;
        }

        // Flagship: 5 published chapters; chapter 3 gets an alternate version.
        Story flagship = stories[0];
        for (int n = 1; n <= 5; n++)
        {
            Chapter ch = AddChapter(flagship, n, $"Chapter {n}: Seed Title {n}", published: true, paragraphs: 3);
            if (n == 3)
            {
                string altHtml = Body("alternate version of chapter 3", 4);
                ch.ChapterContents.Add(new ChapterContent
                {
                    AuthorId = flagship.AuthorId,
                    VersionName = "Alternate Seed Version",
                    ChapterText = altHtml,
                    WordCount = ChapterText.CountWords(altHtml),
                    SortOrder = 2,
                    Rating = null,
                    PublishDate = Now.AddDays(-30),
                });
                ch.VersionCount = 2;
            }
        }

        // Draft-chapter story: 2 published + 1 unpublished draft.
        Story draftStory = stories[1];
        AddChapter(draftStory, 1, "Chapter 1: Seed Published", published: true, paragraphs: 3);
        AddChapter(draftStory, 2, "Chapter 2: Seed Published", published: true, paragraphs: 2);
        AddChapter(draftStory, 3, "Chapter 3: Seed Draft (unpublished)", published: false, paragraphs: 2);

        // Every other non-draft story gets one published chapter so reading pages work.
        foreach (Story story in stories.Skip(2).Where(s => s.StoryStatusId != StoryStatusEnum.Draft))
            AddChapter(story, 1, "Chapter 1: Seed Chapter", published: true, paragraphs: 2);

        await context.SaveChangesAsync();

        // PrimaryContentId is only known post-save (Chapter ⇄ ChapterContent circular FK).
        foreach ((Chapter chapter, ChapterContent primary) in fixups)
            chapter.PrimaryContentId = primary.ChapterContentId;
        await context.SaveChangesAsync();
    }

    // ── Social graph: follows, vouches, bookshelf interactions ──────────────────

    private async Task SeedSocialGraphAsync(SeedUsers users, List<Story> stories)
    {
        context.FollowedUsers.AddRange(
            new FollowedUser { User = users.Test, FollowedUserNavigation = users.AuthorAlpha, DateFollowed = Now.AddDays(-20), ReceiveAlerts = true },
            new FollowedUser { User = users.Test, FollowedUserNavigation = users.AuthorBeta, DateFollowed = Now.AddDays(-18), ReceiveAlerts = false },
            new FollowedUser { User = users.ReaderGamma, FollowedUserNavigation = users.AuthorAlpha, DateFollowed = Now.AddDays(-15), ReceiveAlerts = true },
            new FollowedUser { User = users.AuthorAlpha, FollowedUserNavigation = users.Test, DateFollowed = Now.AddDays(-12), ReceiveAlerts = true });

        context.Vouches.AddRange(
            new Vouch
            {
                VouchingUser = users.AuthorAlpha, VouchedUser = users.Test,
                VouchText = "Seed vouch: reliable beta reader.", DateVouched = Now.AddDays(-9),
            },
            new Vouch
            {
                VouchingUser = users.Test, VouchedUser = users.AuthorBeta,
                VouchText = "Seed vouch: consistent update schedule.", DateVouched = Now.AddDays(-7),
            });

        // TestUser bookshelf coverage — one row per tab semantic. ActivelyReading = HasStarted
        // && !IsCompleted && !IsIgnored; Abandoned = IsIgnored && HasStarted.
        void Usi(User user, Story story, Action<UserStoryInteraction> set)
        {
            var usi = new UserStoryInteraction { User = user, Story = story };
            set(usi);
            // Shared-PK one-to-one — EF propagates (UserId, StoryId) from the principal USI on save.
            usi.InteractionDatePartition = new UserStoryInteractionDate
            {
                FavoriteDate = usi.IsFavorite ? Now.AddDays(-5) : null,
                HiddenFavoriteDate = usi.IsHiddenFavorite ? Now.AddDays(-5) : null,
                FollowedDate = usi.IsFollowed ? Now.AddDays(-6) : null,
                ReadItLaterDate = usi.IsReadItLater ? Now.AddDays(-4) : null,
                IgnoredDate = usi.IsIgnored ? Now.AddDays(-3) : null,
                CompletedDate = usi.IsCompleted ? Now.AddDays(-2) : null,
            };
            context.UserStoryInteractions.Add(usi);
        }

        Usi(users.Test, stories[0], u => { u.IsFavorite = true; u.HasStarted = true; });            // Favorites + ActivelyReading
        Usi(users.Test, stories[3], u => { u.IsHiddenFavorite = true; u.IsFavorite = true; });      // Private Favorites
        Usi(users.Test, stories[1], u => { u.IsFollowed = true; u.HasStarted = true; });            // Following
        Usi(users.Test, stories[2], u => { u.IsCompleted = true; u.HasStarted = true; });           // Completed
        Usi(users.Test, stories[4], u => u.IsReadItLater = true);                                   // Read It Later
        Usi(users.Test, stories[11], u => u.IsIgnored = true);                                      // Ignored
        Usi(users.Test, stories[6], u => { u.IsIgnored = true; u.HasStarted = true; });             // Abandoned
        Usi(users.ReaderGamma, stories[0], u => { u.IsFavorite = true; u.HasStarted = true; });
        Usi(users.ReaderGamma, stories[2], u => { u.IsCompleted = true; u.HasStarted = true; });

        await context.SaveChangesAsync();
    }

    // ── Comments + recommendations ───────────────────────────────────────────────

    private async Task SeedCommentsAndRecommendationsAsync(SeedUsers users, List<Story> stories)
    {
        Chapter flagshipCh1 = await context.Chapters
            .FirstAsync(ch => ch.StoryId == stories[0].StoryId && ch.ChapterNumber == 1);

        var root = new ChapterComment
        {
            UserId = users.Test.Id, ChapterId = flagshipCh1.ChapterId,
            CommentText = "<p>Seed comment: root, with one like.</p>",
            DatePosted = Now.AddDays(-6), LikeCount = 1,
        };
        context.ChapterComments.Add(root);
        await context.SaveChangesAsync(); // materialize root id for the reply + like

        context.ChapterComments.AddRange(
            new ChapterComment
            {
                UserId = users.AuthorAlpha.Id, ChapterId = flagshipCh1.ChapterId,
                ParentCommentId = root.CommentId,
                CommentText = "<p>Seed comment: reply depth 1.</p>",
                DatePosted = Now.AddDays(-5),
            },
            new ChapterComment
            {
                UserId = users.ReaderGamma.Id, ChapterId = flagshipCh1.ChapterId,
                CommentText = "<p>Seed comment: marked as a spoiler.</p>",
                DatePosted = Now.AddDays(-4), IsSpoiler = true,
            });
        context.CommentLikes.Add(new CommentLike { CommentId = root.CommentId, UserId = users.AuthorAlpha.Id });

        context.UserProfileComments.Add(new UserProfileComment
        {
            UserId = users.AuthorAlpha.Id, ProfileUserId = users.Test.Id,
            CommentText = "<p>Seed comment: on TestUser's profile wall.</p>",
            DatePosted = Now.AddDays(-3),
        });

        // Recommendations: approved (StatusId 2 via HasData'd RecommendationStatus).
        const short approved = 2;
        context.Recommendations.AddRange(
            new Recommendation
            {
                StoryId = stories[0].StoryId, RecommenderId = users.Test.Id, StatusId = approved,
                IsHiddenGem = false, IsHighlightedByAuthor = true, LikeCount = 1,
                DatePosted = Now.AddDays(-8),
                RecommendationDetail = new RecommendationDetail
                    { Text = "<p>Seed recommendation: highlighted by the author, one like.</p>" },
                Likes = { new RecommendationLike { UserId = users.ReaderGamma.Id } },
            },
            new Recommendation
            {
                StoryId = stories[2].StoryId, RecommenderId = users.ReaderGamma.Id, StatusId = approved,
                IsHiddenGem = true, DatePosted = Now.AddDays(-6),
                RecommendationDetail = new RecommendationDetail
                    { Text = "<p>Seed recommendation: designated a Hidden Gem.</p>" },
            },
            new Recommendation
            {
                StoryId = stories[4].StoryId, RecommenderId = users.AuthorAlpha.Id, StatusId = approved,
                DatePosted = Now.AddDays(-2),
                RecommendationDetail = new RecommendationDetail
                    { Text = "<p>Seed recommendation: plain approved entry.</p>" },
            });

        await context.SaveChangesAsync();
    }

    // ── Groups ───────────────────────────────────────────────────────────────────

    private async Task SeedGroupsAsync(SeedUsers users, List<Story> stories)
    {
        var standard = new Group
        {
            Creator = users.AuthorAlpha,
            GroupName = "Seed Group: Standard",
            Description = "Seed group with members, folders, stories, a blog post, and a comment.",
            AudienceRating = Rating.E, MaxContentRating = Rating.M,
            DateCreated = Now.AddDays(-25),
            GroupMembers =
            {
                new GroupMember { User = users.AuthorAlpha, Role = GroupRole.Admin, DateJoined = Now.AddDays(-25) },
                new GroupMember { User = users.Test, Role = GroupRole.Member, DateJoined = Now.AddDays(-20), NotifyForNewBlogPost = true },
                new GroupMember { User = users.ReaderGamma, Role = GroupRole.Member, DateJoined = Now.AddDays(-15) },
            },
        };
        var sfw = new Group
        {
            Creator = users.ReaderGamma,
            GroupName = "Seed Group: SFW Only",
            Description = "Seed group capped below Mature content.",
            // (E, T) is the canonical SfwOnly pair — see GroupAudienceTypeMapper.
            AudienceRating = Rating.E, MaxContentRating = Rating.T,
            DateCreated = Now.AddDays(-22),
            GroupMembers = { new GroupMember { User = users.ReaderGamma, Role = GroupRole.Admin, DateJoined = Now.AddDays(-22) } },
        };
        var mature = new Group
        {
            Creator = users.AuthorBeta,
            GroupName = "Seed Group: Mature",
            Description = "Seed mature-audience group — invisible to mature-off viewers.",
            AudienceRating = Rating.M, MaxContentRating = Rating.M,
            DateCreated = Now.AddDays(-19),
            GroupMembers = { new GroupMember { User = users.AuthorBeta, Role = GroupRole.Admin, DateJoined = Now.AddDays(-19) } },
        };
        context.Groups.AddRange(standard, sfw, mature);
        await context.SaveChangesAsync();

        var parentFolder = new GroupFolder
        {
            GroupId = standard.GroupId, Name = "Seed Folder: Top", MaxRating = Rating.M, SortOrder = 1,
        };
        context.GroupFolders.Add(parentFolder);
        await context.SaveChangesAsync();

        context.GroupFolders.Add(new GroupFolder
        {
            GroupId = standard.GroupId, Name = "Seed Folder: Nested", MaxRating = Rating.T,
            SortOrder = 1, ParentFolderId = parentFolder.GroupFolderId,
        });
        context.GroupStories.AddRange(
            new GroupStory
            {
                GroupId = standard.GroupId, StoryId = stories[0].StoryId,
                AddedByUserId = users.AuthorAlpha.Id, DateAdded = Now.AddDays(-14),
            },
            new GroupStory
            {
                GroupId = standard.GroupId, StoryId = stories[2].StoryId,
                AddedByUserId = users.Test.Id, DateAdded = Now.AddDays(-10),
            });
        context.GroupComments.Add(new GroupComment
        {
            UserId = users.Test.Id, GroupId = standard.GroupId,
            CommentText = "<p>Seed comment: on the standard group's wall.</p>",
            DatePosted = Now.AddDays(-8),
        });
        context.GroupBlogPosts.Add(new GroupBlogPost
        {
            GroupId = standard.GroupId, AuthorId = users.AuthorAlpha.Id,
            Title = "Seed Group Blog Post",
            Content = "<p>Seed group blog post body.</p>",
            Rating = Rating.E, IsPublished = true,
            DateCreated = Now.AddDays(-7), LastUpdatedDate = Now.AddDays(-7),
        });

        await context.SaveChangesAsync();
    }

    // ── Blog posts ───────────────────────────────────────────────────────────────

    private async Task SeedBlogPostsAsync(SeedUsers users, List<Story> stories)
    {
        context.ProfileBlogPosts.AddRange(
            new ProfileBlogPost
            {
                AuthorId = users.AuthorAlpha.Id,
                Title = "Seed Blog Post: Published, Linked Story",
                Content = "<p>Seed profile blog post body, linked to the flagship story.</p>",
                Rating = Rating.E, IsPublished = true, StoryId = stories[0].StoryId,
                DateCreated = Now.AddDays(-11), LastUpdatedDate = Now.AddDays(-11),
            },
            new ProfileBlogPost
            {
                AuthorId = users.AuthorAlpha.Id,
                Title = "Seed Blog Post: Draft",
                Content = "<p>Seed draft blog post body (unpublished).</p>",
                Rating = Rating.E, IsPublished = false,
                DateCreated = Now.AddDays(-2), LastUpdatedDate = Now.AddDays(-1),
            });
        await context.SaveChangesAsync();
    }

    // ── Messaging ────────────────────────────────────────────────────────────────

    private async Task SeedMessagingAsync(SeedUsers users)
    {
        var convo = new Conversation
        {
            Subject = "Seed conversation",
            DateCreated = Now.AddDays(-5),
        };
        context.Conversations.Add(convo);
        await context.SaveChangesAsync();

        DateTime lastReadByTest = Now.AddDays(-4); // TestUser has read up to message 2 → 1 unread
        context.ConversationParticipants.AddRange(
            new ConversationParticipant { ConversationId = convo.ConversationId, UserId = users.Test.Id, LastReadTimestamp = lastReadByTest },
            new ConversationParticipant { ConversationId = convo.ConversationId, UserId = users.AuthorAlpha.Id, LastReadTimestamp = Now.AddDays(-1) });
        context.PrivateMessages.AddRange(
            new PrivateMessage
            {
                ConversationId = convo.ConversationId, SenderUserId = users.Test.Id,
                MessageText = "<p>Seed message 1 (from TestUser).</p>", DateSent = Now.AddDays(-5),
            },
            new PrivateMessage
            {
                ConversationId = convo.ConversationId, SenderUserId = users.AuthorAlpha.Id,
                MessageText = "<p>Seed message 2 (reply).</p>", DateSent = Now.AddDays(-4).AddHours(-1),
            },
            new PrivateMessage
            {
                ConversationId = convo.ConversationId, SenderUserId = users.AuthorAlpha.Id,
                MessageText = "<p>Seed message 3 — unread by TestUser (envelope badge shows 1).</p>",
                DateSent = Now.AddDays(-1),
            });
        await context.SaveChangesAsync();
    }

    // ── Notifications + reports ──────────────────────────────────────────────────

    private async Task SeedNotificationsAndReportsAsync(SeedUsers users, List<Story> stories)
    {
        int hiddenGemRecId = await context.Recommendations
            .Where(r => r.IsHiddenGem).Select(r => r.RecommendationId).FirstAsync();

        context.Notifications.AddRange(
            new Notification
            {
                RecipientUserId = users.Test.Id, NotificationTypeId = NotificationTypeEnum.SiteAnnouncement,
                RelatedEntityId = 0, IsRead = false, DateCreated = Now.AddDays(-10),
            },
            new Notification
            {
                RecipientUserId = users.Test.Id, NotificationTypeId = NotificationTypeEnum.NewFollowerOnYou,
                SourceUserId = users.AuthorAlpha.Id, RelatedEntityId = users.AuthorAlpha.Id,
                IsRead = false, DateCreated = Now.AddDays(-12),
            },
            new Notification
            {
                RecipientUserId = users.Test.Id, NotificationTypeId = NotificationTypeEnum.HiddenGem,
                SourceUserId = users.ReaderGamma.Id, RelatedEntityId = users.ReaderGamma.Id,
                IsRead = false, DateCreated = Now.AddDays(-6),
            });

        long commentId = await context.ChapterComments
            .OrderBy(c2 => c2.CommentId).Select(c2 => c2.CommentId).FirstAsync();

        // Reports: keep target ActiveReportCount in sync by construction.
        Story reportedStory = stories[4];
        reportedStory.ActiveReportCount = 1;
        context.Reports.AddRange(
            new Report
            {
                ReporterUserId = users.ReaderGamma.Id,
                ReportedEntityType = ReportedEntityType.Story,
                ReportedEntityId = reportedStory.StoryId,
                ReportReasonId = 2, // Spam (HasData'd report reason)
                Notes = "Seed report against a story.",
                ReportStatusId = ReportStatusEnum.Open,
                DateReported = Now.AddDays(-3),
            },
            new Report
            {
                ReporterUserId = users.AuthorBeta.Id,
                ReportedEntityType = ReportedEntityType.Comment,
                ReportedEntityId = commentId,
                ReportReasonId = 1, // Other
                Notes = "Seed report against a comment.",
                ReportStatusId = ReportStatusEnum.Open,
                DateReported = Now.AddDays(-1),
            });
        ChapterComment reported = await context.ChapterComments.FirstAsync(c2 => c2.CommentId == commentId);
        reported.ActiveReportCount = 1;

        await context.SaveChangesAsync();
    }

    // ── Profiles + stats ─────────────────────────────────────────────────────────

    private async Task SeedProfilesAndStatsAsync(SeedUsers users)
    {
        context.UserProfiles.AddRange(
            new UserProfile { UserId = users.Test.Id, Text = "<p>Seed bio for TestUser.</p>" },
            new UserProfile { UserId = users.AuthorAlpha.Id, Text = "<p>Seed bio for AuthorAlpha.</p>" });

        // Counters mirror the seeded content above (maintained by construction — see header).
        context.UserStats.AddRange(
            new UserStat
            {
                UserId = users.Test.Id,
                StoriesWritten = 2, CommentsWritten = 3, RecommendationsWritten = 1,
                StoriesRead = 1, StoriesInProgress = 2, StoriesIgnored = 2,
                FollowerCount = 1, AuthorsFollowed = 2, FavoritesOnStories = 0,
            },
            new UserStat
            {
                UserId = users.AuthorAlpha.Id,
                StoriesWritten = 5, WordsWritten = 400, CommentsWritten = 2, BlogPostsWritten = 3,
                FollowerCount = 2, AuthorsFollowed = 1, FavoritesOnStories = 2,
                RecommendationsReceived = 1, GroupsJoined = 1,
            },
            new UserStat
            {
                UserId = users.AuthorBeta.Id,
                StoriesWritten = 5, WordsWritten = 250, FollowerCount = 0, AuthorsFollowed = 0,
                RecommendationsReceived = 1,
            });

        // One earned badge for TestUser so the badge curation UI (settings) and the UserCard
        // badge row render a populated state; visible by default (DisplayOrder 1). Award
        // thresholds themselves are Integration-covered — this row is display seed only.
        context.UserBadges.Add(new UserBadge
        {
            UserId = users.Test.Id,
            BadgeKey = SiteBadges.Recommender,
            DisplayOrder = 1,
            DateEarned = Now.AddDays(-4),
        });

        await context.SaveChangesAsync();
    }
}
