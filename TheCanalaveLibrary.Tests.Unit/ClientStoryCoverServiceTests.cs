using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using TheCanalaveLibrary.Client;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// The WASM side of the story cover's surfaces (owner ruling D14, WU-BlobCleanup; added by its review fixes,
/// 2026-10-13) — <see cref="ClientStoryWriteService"/>/<see cref="ClientStoryReadService"/> constructed
/// directly over a canned <see cref="HttpMessageHandler"/>, no host (the <c>ClientStoryLifecycleServiceTests</c>
/// shape). Pins the verb and route of <c>RemoveCoverArtAsync</c> and <c>UploadCoverArtAsync</c> against
/// <c>StoryEndpoints</c>, their status → exception translation, and that the edit read deserializes the
/// cover echo into <see cref="StoryEditDTO"/> beside the polymorphic tag list. The server routes are
/// Integration-covered (<c>StoryEndpointsTests</c>); these are the client impls the browser runs on WASM.
/// Tier: Unit.
/// </summary>
public class ClientStoryCoverServiceTests
{
    private const string CoverPath = "/uploads/stories/7/cover-0f8fad5b-d9cb-469f-a165-70867728950e.png";

    [Fact]
    public async Task RemoveCoverArtAsync_SendsABodilessDelete_ToTheCoverRoute()
    {
        var handler = new CannedHandler(HttpStatusCode.NoContent, "");
        ClientStoryWriteService svc = new(NewClient(handler));

        await svc.RemoveCoverArtAsync(7);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Delete);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/stories/7/cover");
        handler.LastBody.Should().BeNull("clearing a cover is its own bodiless call");
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, typeof(UnauthorizedAccessException))]
    [InlineData(HttpStatusCode.Unauthorized, typeof(SessionExpiredException))]
    [InlineData(HttpStatusCode.NotFound, typeof(KeyNotFoundException))]
    public async Task RemoveCoverArtAsync_TranslatesTheRefusal(HttpStatusCode status, Type expected)
    {
        ClientStoryWriteService svc = new(NewClient(new CannedHandler(status, "")));

        Func<Task> act = () => svc.RemoveCoverArtAsync(7);

        (await act.Should().ThrowAsync<Exception>()).Which.Should().BeOfType(expected);
    }

    [Fact]
    public async Task UploadCoverArtAsync_PostsOneMultipartFilePart_AndReturnsTheStoredPath()
    {
        var handler = new CannedHandler(HttpStatusCode.OK, JsonSerializer.Serialize(CoverPath));
        ClientStoryWriteService svc = new(NewClient(handler));
        using MemoryStream content = new([0x89, 0x50, 0x4E, 0x47]);

        string path = await svc.UploadCoverArtAsync(content, "image/png", 7);

        path.Should().Be(CoverPath, "the method persists the cover and answers with its path (D14)");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/stories/7/cover");
        handler.LastContentType.Should().Be("multipart/form-data");
        handler.LastBody.Should().Contain("name=file").And.Contain("Content-Type: image/png");
    }

    [Fact]
    public async Task UploadCoverArtAsync_400_ThrowsStoryValidationException_WithTheServerText()
    {
        var handler = new CannedHandler(HttpStatusCode.BadRequest, """{"detail":"This file isn't a JPEG, PNG or WebP image."}""");
        ClientStoryWriteService svc = new(NewClient(handler));
        using MemoryStream content = new([1, 2, 3]);

        Func<Task> act = () => svc.UploadCoverArtAsync(content, "image/png", 7);

        (await act.Should().ThrowAsync<StoryValidationException>())
            .Which.Errors.Should().Equal("This file isn't a JPEG, PNG or WebP image.");
    }

    [Fact]
    public async Task GetStoryForEditAsync_ReadsTheCoverEcho_BesideThePolymorphicTags()
    {
        // The server serializes the declared StoryEditDTO with the web defaults; IStoryTag rides the base
        // class's [JsonPolymorphic] list, so the subclass must round-trip it on WASM.
        StoryEditDTO sent = new()
        {
            StoryId = 7,
            Title = "Covered",
            CoverArtRelativeUrl = CoverPath,
            StoryTags = [new StoryTagDTO { TagId = 12, TagTypeEnum = TagTypeEnum.Genre, Priority = TagPriority.Supporting }],
        };
        var handler = new CannedHandler(HttpStatusCode.OK, JsonSerializer.Serialize(sent, JsonSerializerOptions.Web));
        ClientStoryReadService svc = new(NewClient(handler));

        StoryEditDTO? read = await svc.GetStoryForEditAsync(7);

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/stories/7/edit");
        read.Should().NotBeNull();
        read!.CoverArtRelativeUrl.Should().Be(CoverPath);
        read.StoryTags.Should().ContainSingle().Which.Should().BeOfType<StoryTagDTO>()
            .Which.TagId.Should().Be(12);
    }

    // ── Plumbing ──────────────────────────────────────────────────────────────

    private static HttpClient NewClient(CannedHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://localhost/") };

    /// <summary>Returns one canned response and records the last request, its media type and its body
    /// (read inside <c>SendAsync</c>: the caller disposes the content when the call returns).</summary>
    private sealed class CannedHandler(HttpStatusCode status, string jsonBody) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastContentType { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastContentType = request.Content?.Headers.ContentType?.MediaType;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
