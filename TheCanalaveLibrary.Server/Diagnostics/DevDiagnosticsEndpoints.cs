using Microsoft.AspNetCore.Identity;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Home for Development-only diagnostic endpoints used to exercise service-layer code paths
/// directly during local verification (e.g. account deletion, where driving the real UI flow
/// requires logging in as the target user). Never mapped outside Development — see the
/// <c>app.Environment.IsDevelopment()</c> guard around <see cref="MapDevDiagnosticsEndpoints"/>
/// in Program.cs. Add new ad-hoc verification endpoints here rather than inline in Program.cs or
/// scattered across feature endpoint files.
/// </summary>
public static class DevDiagnosticsEndpoints
{
    public static void MapDevDiagnosticsEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder devApi = app.MapGroup("/dev");

        devApi.MapPost("/test-delete-user/{id:int}", async (int id, UserDeletionService deletionService) =>
            await deletionService.DeleteUserAsync(id) ? Results.Ok("deleted") : Results.NotFound());

        // --- WU12 verification — throwaway, removed once confirmed (plan: "removed after") ---

        devApi.MapGet("/wu12/whoami", (HttpContext http, IActiveUserContext activeUser) => Results.Ok(new
        {
            httpClaims = http.User.Claims.Select(c => new { c.Type, c.Value }),
            activeUser = new { activeUser.UserId, activeUser.IsAuthenticated, activeUser.ShowMatureContent, activeUser.Theme, activeUser.PrefersAnimatedSprites }
        }));

        // GET + redirect (not POST + JS fetch): DevLoginBar renders plain <a> links to this, which
        // works in every render mode and regardless of circuit state. The previous fetch-POST +
        // forceLoad-reload pattern silently failed on an established interactive circuit (the POST
        // never reached the server — found via browser pass 2026-07-01). GET-with-side-effects is
        // acceptable here only because this endpoint is Development-only and never shipped.
        devApi.MapGet("/wu12/login-as/{username}", async (string username, SignInManager<User> signInManager, UserManager<User> userManager) =>
        {
            User? user = await userManager.FindByNameAsync(username);
            if (user is null) return Results.NotFound($"no user named {username}");
            await signInManager.SignInAsync(user, isPersistent: false);
            return Results.Redirect("/");
        });

        devApi.MapPost("/wu12/create-test-story", async (
            string title, string rating, int settingTagId, int genreTagId,
            IStoryWriteService writeService, UserManager<User> userManager) =>
        {
            User? author = await userManager.FindByNameAsync("TestUser");
            if (author is null) return Results.NotFound("seed TestUser first");

            // AuthorId is server-stamped in CreateStoryAsync from IActiveUserContext — not passed here.
            CreateStoryDTO dto = new()
            {
                Title = title,
                ShortDescription = "WU12 diagnostic story",
                Rating = Enum.Parse<Rating>(rating),
                StoryStatusId = StoryStatusEnum.InProgress,
                LongDescription = "WU12 diagnostic long description",
                PostApprovalStatus = StoryStatusEnum.InProgress,
                StoryTags =
                [
                    new StoryTagDTO { TagId = settingTagId, Priority = TagPriority.Primary, TagTypeEnum = TagTypeEnum.Setting },
                    new StoryTagDTO { TagId = genreTagId, Priority = TagPriority.Primary, TagTypeEnum = TagTypeEnum.Genre }
                ]
            };

            int storyId = await writeService.CreateStoryAsync(dto);
            return Results.Ok(new { storyId });
        });

        devApi.MapGet("/wu12/listings/recent", async (int page, int pageSize, IStoryReadService readService) =>
        {
            (StoryListingDto[] items, int totalCount) = await readService.GetRecentListingsAsync(page, pageSize);
            return Results.Ok(new
            {
                totalCount,
                items = items.Select(i => new { i.StoryId, i.Title, i.Rating, i.WordCount, tagCount = i.Tags.Count })
            });
        });

        devApi.MapGet("/wu12/listings/by-ids", async (string ids, IStoryReadService readService) =>
        {
            int[] storyIds = ids.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
            StoryListingDto[] items = await readService.GetListingsByIdsAsync(storyIds);
            return Results.Ok(items.Select(i => new { i.StoryId, i.Title, i.Rating }));
        });

        devApi.MapPost("/wu12/upload-test-image", async (IImageStorageService imageStorage) =>
        {
            // Minimal valid 1x1 PNG.
            byte[] pngBytes = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
            using MemoryStream ms = new(pngBytes);
            string relativePath = await imageStorage.SaveAsync(ms, "image/png", ImageKind.Cover, 999);
            return Results.Ok(new { relativePath });
        });

        // --- WU-Marts discovery probes — the headless horizontal-line surface (testing.md:
        // these assert nothing; a human reads the JSON and judges). ---

        // On-demand mart rebuild (the daily worker does this at 03:00 UTC; this lets a human
        // rebuild right after running the SeedTool instead of waiting).
        devApi.MapPost("/marts/rebuild", async (DiscoveryMartRebuilder rebuilder, CancellationToken ct) =>
        {
            (long treeEdges, long alsoFavorited, long alsoRecommended) = await rebuilder.RebuildAllAsync(ct);
            return Results.Ok(new { treeEdges, alsoFavorited, alsoRecommended });
        });

        // F59 Automatic Tree Search. edges = comma-separated TreeSearchEdgeType names or values
        // (e.g. "HiddenGem,AuthorSpotlight" or "4,5"); sort = Random | ByDegree.
        devApi.MapGet("/discovery/tree-search", async (
            int? rootStoryId, int? rootUserId, int maxDegrees, string edges,
            bool? includePaths, string? sort, int? resultCap,
            ITreeSearchReadService treeSearch, CancellationToken ct) =>
        {
            TreeSearchRequest request = new()
            {
                RootStoryId = rootStoryId,
                RootUserId = rootUserId,
                MaxDegrees = maxDegrees,
                EdgeTypes = edges.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(e => Enum.Parse<TreeSearchEdgeType>(e, ignoreCase: true)).ToList(),
                IncludePaths = includePaths ?? false,
                Sort = sort is null ? TreeSearchSortOrder.Random : Enum.Parse<TreeSearchSortOrder>(sort, ignoreCase: true),
                ResultCap = resultCap ?? 100,
            };
            try
            {
                TreeSearchResultDto result = await treeSearch.TraverseAsync(request, ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        // F61 co-occurrence reads.
        devApi.MapGet("/discovery/also-favorited/{storyId:int}", async (
                int storyId, int? take, ICoOccurrenceReadService coOccurrence, CancellationToken ct) =>
            Results.Ok(await coOccurrence.GetAlsoFavoritedAsync(storyId, take ?? 10, ct)));

        devApi.MapGet("/discovery/also-recommended/{storyId:int}", async (
                int storyId, int? take, ICoOccurrenceReadService coOccurrence, CancellationToken ct) =>
            Results.Ok(await coOccurrence.GetAlsoRecommendedAsync(storyId, take ?? 10, ct)));

        // WU-SiteDailyStat (Feature 62): on-demand upsert — the daily worker does this at 03:00
        // UTC; this lets a human trigger it right after seeding instead of waiting. date defaults
        // to "yesterday" UTC (the normal daily target); pass yyyy-MM-dd to upsert a specific day
        // (e.g. re-running today's partial day while iterating).
        devApi.MapPost("/marts/site-daily-stat", async (
            string? date, SiteDailyStatAggregator aggregator, CancellationToken ct) =>
        {
            DateOnly statDate = date is null
                ? SiteDailyStatWorker.PreviousCompletedUtcDay(DateTime.UtcNow)
                : DateOnly.Parse(date);
            await aggregator.UpsertDayAsync(statDate, ct);
            return Results.Ok(new { statDate });
        });
    }
}
