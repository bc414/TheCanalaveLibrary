using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using TheCanalaveLibrary.Client;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="ClientTagReadService"/> / <see cref="ClientTagWriteService"/>
/// (WU-L5Pilot) — constructed directly over a canned <see cref="HttpMessageHandler"/>, no host.
/// The behavior worth pinning is the Layer-5 boundary translation: request URL/verb shapes and,
/// above all, the status-code → contract-exception mapping that keeps components behaving
/// identically on either side of the ITag*Service interfaces. Tier: Unit.
/// </summary>
public class ClientTagServiceTests
{
    // ── Reads ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTagDirectoryAsync_GetsDirectoryRouteAndDeserializes()
    {
        var handler = new CannedHandler(HttpStatusCode.OK,
            """[{"tagType":2,"nodes":[{"tag":{"tagId":7,"tagName":"Drama","tagTypeId":2},"children":[]}]}]""");
        ClientTagReadService svc = new(NewClient(handler));

        List<TagDirectoryGroupDto> groups = await svc.GetTagDirectoryAsync();

        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/tags/directory");
        groups.Should().ContainSingle(g => g.TagType == TagTypeEnum.Genre);
        groups[0].Nodes[0].Tag.TagName.Should().Be("Drama");
    }

    [Fact]
    public async Task SearchTagChipsAsync_BlankTerm_ShortCircuitsWithoutRequest()
    {
        var handler = new CannedHandler(HttpStatusCode.OK, "[]");
        ClientTagReadService svc = new(NewClient(handler));

        List<TagChipDto> chips = await svc.SearchTagChipsAsync(TagTypeEnum.Character, "   ");

        chips.Should().BeEmpty();
        handler.LastRequest.Should().BeNull(); // mirrors the server impl's no-op contract
    }

    [Fact]
    public async Task GetTagChipsByIdsAsync_EncodesRepeatedIdsKeys()
    {
        var handler = new CannedHandler(HttpStatusCode.OK, "[]");
        ClientTagReadService svc = new(NewClient(handler));

        await svc.GetTagChipsByIdsAsync([5, 9]);

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/tags/chips/by-ids?ids=5&ids=9");
    }

    // ── Write happy paths ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTagAsync_Ok_ReturnsTagSaveResult()
    {
        var handler = new CannedHandler(HttpStatusCode.OK, """{"tagId":42,"spriteWarning":"no asset"}""");
        ClientTagWriteService svc = new(NewClient(handler));

        TagSaveResult result = await svc.CreateTagAsync(NewCreateDto("Fresh"));

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/tags");
        result.TagId.Should().Be(42);
        result.SpriteWarning.Should().Be("no asset");
    }

    [Fact]
    public async Task UpdateTagAsync_OkWithJsonNullBody_ReturnsNullWarning()
    {
        var handler = new CannedHandler(HttpStatusCode.OK, "null"); // raw string?/JSON-null contract
        ClientTagWriteService svc = new(NewClient(handler));

        string? warning = await svc.UpdateTagAsync(NewUpdateDto(tagId: 8));

        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/tags/8");
        warning.Should().BeNull();
    }

    [Fact]
    public async Task DeleteTagAsync_NoContent_Completes()
    {
        var handler = new CannedHandler(HttpStatusCode.NoContent, "");
        ClientTagWriteService svc = new(NewClient(handler));

        await svc.DeleteTagAsync(3);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Delete);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/tags/3");
    }

    // ── Status-code → contract-exception translation ──────────────────────────

    [Fact]
    public async Task CreateTagAsync_400_ThrowsTagValidationExceptionWithProblemDetail()
    {
        var handler = new CannedHandler(HttpStatusCode.BadRequest,
            """{"title":"Bad Request","status":400,"detail":"A Genre tag named \"Dup\" already exists."}""");
        ClientTagWriteService svc = new(NewClient(handler));

        Func<Task> act = () => svc.CreateTagAsync(NewCreateDto("Dup"));

        (await act.Should().ThrowAsync<TagValidationException>())
            .WithMessage("""A Genre tag named "Dup" already exists.""");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task CreateTagAsync_AuthFailure_ThrowsUnauthorizedAccessException(HttpStatusCode status)
    {
        var handler = new CannedHandler(status, "");
        ClientTagWriteService svc = new(NewClient(handler));

        Func<Task> act = () => svc.CreateTagAsync(NewCreateDto("Blocked"));

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task UpdateTagAsync_404_ThrowsKeyNotFoundException()
    {
        var handler = new CannedHandler(HttpStatusCode.NotFound, "");
        ClientTagWriteService svc = new(NewClient(handler));

        Func<Task> act = () => svc.UpdateTagAsync(NewUpdateDto(tagId: 999));

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task DeleteTagAsync_UnmappedFailureStatus_ThrowsHttpRequestException()
    {
        var handler = new CannedHandler(HttpStatusCode.InternalServerError, "");
        ClientTagWriteService svc = new(NewClient(handler));

        Func<Task> act = () => svc.DeleteTagAsync(3);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // ── Plumbing ──────────────────────────────────────────────────────────────

    private static HttpClient NewClient(CannedHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://localhost/") };

    private static CreateTagDto NewCreateDto(string name) =>
        new() { TagName = name, TagTypeId = TagTypeEnum.Genre };

    private static UpdateTagDto NewUpdateDto(int tagId) =>
        new() { TagId = tagId, TagName = "Renamed", TagTypeId = TagTypeEnum.Genre };

    /// <summary>Returns one canned response and records the last request for URL/verb assertions.</summary>
    private sealed class CannedHandler(HttpStatusCode status, string jsonBody) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            });
        }
    }
}
