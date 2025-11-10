using TheCanalaveLibrary.Core.DTOs;
using TheCanalaveLibrary.Core.ServiceInterfaces;

namespace TheCanalaveLibrary.Endpoints;

public static class StoryEndpoints
{
    public static void MapStoryEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder storiesApi = app.MapGroup("/api/stories");

        storiesApi.MapGet("/{storyId:int}", async (int storyId, IStoryOverviewService storyService) =>
        {
            StoryDetailsDTO? story = await storyService.GetStoryByIdAsync(storyId);
            return story is not null ? Results.Ok(story) : Results.NotFound();
        });

        storiesApi.MapGet("/random-number", async (IStoryOverviewService storyService) =>
        {
            int number = await storyService.GetRandomNumber();
            return Results.Ok(number);
        });
    }
}