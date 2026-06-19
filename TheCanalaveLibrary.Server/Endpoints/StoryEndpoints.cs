using TheCanalaveLibrary.Core.ServiceInterfaces;
using TheCanalaveLibrary.Core.Story;

namespace TheCanalaveLibrary.Server.Endpoints;

public static class StoryEndpoints
{
    public static void MapStoryEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder storiesApi = app.MapGroup("/api/stories");

        storiesApi.MapGet("/{storyId:int}", async (int storyId, IStoryReadService storyService) =>
        {
            StoryDetailsDTO? story = await storyService.GetStoryByIdAsync(storyId);
            return story is not null ? Results.Ok(story) : Results.NotFound();
        });

        /*storiesApi.MapGet("/random-number", async (IStoryReadService storyService) =>
        {
            int number = await storyService.GetRandomNumber();
            return Results.Ok(number);
        });*/
    }
}