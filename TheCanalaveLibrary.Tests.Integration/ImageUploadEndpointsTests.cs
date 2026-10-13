using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// The upload pipeline's refusals over HTTP — the WASM half of the
/// <c>ImageUploadValidationException</c> contract (security.md §"Upload Content Pipeline" Rules).
/// Each refusal must answer 400 with the pipeline's user-written text as
/// <c>ProblemDetails.Detail</c>, which the client services rethrow as their feature's validation
/// exception. Before the WU-DependencyCVEs browser pass (2026-10-12) the pipeline threw
/// <see cref="ArgumentException"/>: the Detail read "Uploaded file is not a recognized image.
/// (Parameter 'buffer')", and the circuit (Unit tier, <c>ImageUploadProcessorTests</c>) showed the
/// generic error. Tier: Integration (the endpoint's exception mapping needs the host).
/// </summary>
[Collection("Postgres")]
public class ImageUploadEndpointsTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task ProfilePicture_BigTiffAdvisoryPayload_Returns400WithTheReadableMessage()
    {
        int userId = await SeedUserAsync("uploader");
        SetActiveUser(userId);

        HttpResponseMessage response = await PostImageAsync(
            "/api/user-settings/profile-picture", BigTiffIfdLoopPayload(), "image/png");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DetailOf(response)).Should().Be(ImageUploadProcessor.UnrecognizedMessage,
            "the Detail is shown to the user verbatim on the WASM phase");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string? stored = await db.Users.Where(u => u.Id == userId)
            .Select(u => u.ProfilePictureRelativeUrl).SingleAsync();
        stored.Should().BeNull("a refused upload writes nothing");
    }

    [Fact]
    public async Task Cover_ClaimedTypeOffTheAllowList_Returns400WithTheReadableMessage()
    {
        int authorId = await SeedUserAsync("author");
        int storyId = await SeedStoryAsync(authorId);
        SetActiveUser(authorId);

        HttpResponseMessage response = await PostImageAsync(
            $"/api/stories/{storyId}/cover", "<svg xmlns=\"http://www.w3.org/2000/svg\"/>"u8.ToArray(),
            "image/svg+xml");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DetailOf(response)).Should().Be(ImageUploadRules.UnsupportedTypeMessage);
    }

    [Fact]
    public async Task Cover_BytesThatAreNotAnImage_Returns400WithTheReadableMessage()
    {
        int authorId = await SeedUserAsync("author");
        int storyId = await SeedStoryAsync(authorId);
        SetActiveUser(authorId);

        HttpResponseMessage response = await PostImageAsync(
            $"/api/stories/{storyId}/cover", "not an image at all"u8.ToArray(), "image/jpeg");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string? detail = await DetailOf(response);
        detail.Should().Be(ImageUploadProcessor.UnrecognizedMessage);
        detail.Should().NotContain("Parameter", "BCL argument text is developer text, never UI copy");
    }

    private async Task<HttpResponseMessage> PostImageAsync(string url, byte[] bytes, string contentType)
    {
        using MultipartFormDataContent form = new();
        ByteArrayContent file = new(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", "upload");
        return await Factory.CreateClient().PostAsync(url, form);
    }

    private static async Task<string?> DetailOf(HttpResponseMessage response)
    {
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return problem.RootElement.TryGetProperty("detail", out JsonElement detail) ? detail.GetString() : null;
    }

    /// <summary>GHSA-wmxv-xphr-5c9g's 24-byte BigTIFF (the same bytes the Unit advisory guard feeds).</summary>
    private static byte[] BigTiffIfdLoopPayload()
    {
        byte[] bigTiff = new byte[24];
        bigTiff[0] = 0x49; bigTiff[1] = 0x49;   // "II"
        bigTiff[2] = 0x2B;                      // BigTIFF magic
        bigTiff[4] = 0x08;                      // 8-byte offsets
        BinaryPrimitives.WriteUInt64LittleEndian(bigTiff.AsSpan(8), 16);
        BinaryPrimitives.WriteUInt64LittleEndian(bigTiff.AsSpan(16), 5_000_000_000);
        return bigTiff;
    }
}
