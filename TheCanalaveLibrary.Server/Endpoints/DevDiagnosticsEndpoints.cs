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

        devApi.MapPost("/wu12/login-as/{username}", async (string username, SignInManager<User> signInManager, UserManager<User> userManager) =>
        {
            User? user = await userManager.FindByNameAsync(username);
            if (user is null) return Results.NotFound($"no user named {username}");
            await signInManager.SignInAsync(user, isPersistent: false);
            return Results.Ok($"signed in as {username}");
        });

        devApi.MapPost("/wu12/create-test-story", async (
            string title, string rating, int settingTagId, int genreTagId,
            IStoryWriteService writeService, UserManager<User> userManager) =>
        {
            User? author = await userManager.FindByNameAsync("TestUser");
            if (author is null) return Results.NotFound("seed TestUser first");

            CreateStoryDTO dto = new()
            {
                AuthorId = author.Id,
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
    }
}
