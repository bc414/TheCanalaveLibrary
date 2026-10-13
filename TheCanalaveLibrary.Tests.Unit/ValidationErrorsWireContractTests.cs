using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Client;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// The validation 400 across the HTTP hop (service audit §2.7.1; WU-ParityAndRemaining, 2026-10-13;
/// layer5-wasm.md §"The Error-Translation Contract"). The server half: <see cref="EndpointHelpers.ExecuteAsync"/>
/// adds an <c>errors</c> string array beside <c>detail</c> for every <see cref="CanalaveValidationException"/>
/// and for nothing else. The client half: <c>ClientHttpHelpers.ThrowIfWriteFailedAsync</c> feeds the
/// feature's factory that array, and falls back to <c>[detail]</c> for any other shape — a framework
/// <c>ValidationProblemDetails</c> dictionary, a mixed array, a detail-only body, a body that is not JSON.
/// Before this, the client rebuilt a one-element list from <c>detail</c>, and for stories that detail is
/// the fixed "Story validation failed.". Client services are constructed over a canned handler, no host
/// (the <see cref="WriteThrottleWireContractTests"/> shape). Tier: Unit.
/// </summary>
public class ValidationErrorsWireContractTests
{
    // ── Server half ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_AValidationException_Answers400_WithItsErrorsBesideTheDetail()
    {
        IResult result = await EndpointHelpers.ExecuteAsync(() =>
            throw new StoryValidationException(["Title is required", "Long description is required"]));

        ProblemHttpResult problem = result.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        problem.ProblemDetails.Detail.Should().Be("Story validation failed.");
        problem.ProblemDetails.Extensions["errors"].Should().BeEquivalentTo(
            new[] { "Title is required", "Long description is required" });
    }

    [Fact]
    public async Task ExecuteAsync_ANonValidation400_StaysDetailOnly()
    {
        IResult result = await EndpointHelpers.ExecuteAsync(() => throw new ArgumentException("Bad shape."));

        ProblemHttpResult problem = result.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        problem.ProblemDetails.Detail.Should().Be("Bad shape.");
        problem.ProblemDetails.Extensions.Should().NotContainKey("errors");
    }

    // ── The round trip ───────────────────────────────────────────────────────────

    [Fact]
    public async Task TheServersBody_RebuildsTheSameList_InTheClientsException()
    {
        string body = await RenderAsync(await EndpointHelpers.ExecuteAsync(() =>
            throw new StoryValidationException(["Title is required", "Your story must have at least one Genre tag selected."])));
        ClientStoryWriteService svc = new(NewClient(new CannedHandler(HttpStatusCode.BadRequest, body)));

        Func<Task> act = () => svc.CreateStoryAsync(new CreateStoryDTO());

        (await act.Should().ThrowAsync<StoryValidationException>())
            .Which.Errors.Should().Equal("Title is required", "Your story must have at least one Genre tag selected.");
    }

    [Fact]
    public async Task ASingleMessageFamily_JoinsTheLines()
    {
        // TagValidationException takes one message; the factory joins the list into it.
        ClientTagWriteService svc = new(NewClient(new CannedHandler(HttpStatusCode.BadRequest,
            """{"status":400,"detail":"a b","errors":["a","b"]}""")));

        Func<Task> act = () => svc.DeleteTagAsync(5);

        (await act.Should().ThrowAsync<TagValidationException>()).Which.Message.Should().Be("a b");
    }

    // ── Tolerant reads: every other shape falls back to the detail ───────────────

    [Theory]
    [InlineData("""{"status":400,"detail":"From the detail.","errors":{"Title":["Required."]}}""")] // framework dictionary
    [InlineData("""{"status":400,"detail":"From the detail.","errors":["ok",3]}""")]                // mixed array
    [InlineData("""{"status":400,"detail":"From the detail.","errors":[]}""")]                      // empty array
    [InlineData("""{"status":400,"detail":"From the detail.","errors":null}""")]                    // null
    [InlineData("""{"status":400,"detail":"From the detail."}""")]                                  // detail only
    public async Task AnyOtherErrorsShape_FallsBackToTheDetail_WithoutAJsonException(string body)
    {
        ClientGroupWriteService svc = new(NewClient(new CannedHandler(HttpStatusCode.BadRequest, body)));

        Func<Task> act = () => svc.CreateFolderAsync(new CreateFolderDto { GroupId = 7, Name = "X" });

        (await act.Should().ThrowAsync<GroupValidationException>())
            .Which.Errors.Should().Equal("From the detail.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Bad Request")]
    public async Task ABodyWithNoProblemDetails_FallsBackToTheGenericLine(string body)
    {
        ClientGroupWriteService svc = new(NewClient(new CannedHandler(HttpStatusCode.BadRequest, body, "text/plain")));

        Func<Task> act = () => svc.CreateFolderAsync(new CreateFolderDto { GroupId = 7, Name = "X" });

        (await act.Should().ThrowAsync<GroupValidationException>())
            .Which.Errors.Should().Equal("The request failed validation.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>Executes a minimal-API result the way the host would and returns the body it wrote.</summary>
    private static async Task<string> RenderAsync(IResult result)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddProblemDetails();
        DefaultHttpContext httpContext = new() { RequestServices = services.BuildServiceProvider() };
        using MemoryStream body = new();
        httpContext.Response.Body = body;

        await result.ExecuteAsync(httpContext);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        return Encoding.UTF8.GetString(body.ToArray());
    }

    private static HttpClient NewClient(HttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("https://localhost/") };

    private sealed class CannedHandler(HttpStatusCode status, string body, string mediaType = "application/problem+json")
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, mediaType)
            });
    }
}
