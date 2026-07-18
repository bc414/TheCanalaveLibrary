using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="UserProfileEndpoints"/> — the Layer-5 HTTP surface over
/// <see cref="IUserProfileReadService"/>, exercised through <c>Factory.CreateClient()</c> so the
/// endpoint's own logic (not just the service) runs for real. Regression coverage for MA-602: the
/// endpoint used to trust a client-supplied <c>includePrivate</c> query bool instead of deriving it
/// from <see cref="IActiveUserContext"/>, letting anyone (including anonymous) read another user's
/// private profile data by passing <c>?includePrivate=true</c>. Tier: Integration.
/// </summary>
[Collection("Postgres")]
public class UserProfileEndpointsTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private async Task SetProfileVisibilityAsync(int userId, ProfileVisibility visibility)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        User user = await db.Users.SingleAsync(u => u.Id == userId);
        user.PrivacySettings.ProfileVisibility = visibility;
        await db.SaveChangesAsync();
    }

    private async Task SetActivityStatusAsync(int userId, bool showActivityStatus, DateTime lastActiveUtc)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        User user = await db.Users.SingleAsync(u => u.Id == userId);
        user.PrivacySettings.ShowActivityStatus = showActivityStatus;
        user.LastActiveUtc = lastActiveUtc;
        await db.SaveChangesAsync();
    }

    // ASP.NET's HttpResultsHelper writes an EMPTY 200 body for a null Results.Json(...) result —
    // ReadFromJsonAsync throws on that. Mirrors ClientHttpHelpers.ReadNullableFromJsonAsync (Client
    // project isn't referenced here) — empty/"null" body maps to default(T).
    private static async Task<T?> ReadNullableAsync<T>(HttpResponseMessage response)
    {
        string raw = await response.Content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(raw) || raw == "null"
            ? default
            : JsonSerializer.Deserialize<T>(raw, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    [Fact]
    public async Task GetProfile_PrivateProfile_AnonymousWithIncludePrivateTrue_StillReturnsNull()
    {
        int ownerId = await SeedUserAsync();
        await SetProfileVisibilityAsync(ownerId, ProfileVisibility.Private);

        HttpClient client = Factory.CreateClient();
        // Attacker-controlled query string — pre-fix, this bypassed the Private gate entirely.
        HttpResponseMessage response = await client.GetAsync($"/api/user-profiles/{ownerId}?includePrivate=true");

        response.EnsureSuccessStatusCode();
        ProfileHeaderDto? header = await ReadNullableAsync<ProfileHeaderDto>(response);
        header.Should().BeNull("a Private profile must stay hidden from anonymous callers regardless of the query string");
    }

    [Fact]
    public async Task GetProfile_PrivateProfile_OtherUserWithIncludePrivateTrue_StillReturnsNull()
    {
        int ownerId = await SeedUserAsync();
        await SetProfileVisibilityAsync(ownerId, ProfileVisibility.Private);
        int otherId = await SeedUserAsync("other");
        SetActiveUser(otherId);

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync($"/api/user-profiles/{ownerId}?includePrivate=true");

        response.EnsureSuccessStatusCode();
        ProfileHeaderDto? header = await ReadNullableAsync<ProfileHeaderDto>(response);
        header.Should().BeNull("only the profile's own owner may bypass the Private gate, never a client-asserted bool");
    }

    [Fact]
    public async Task GetProfile_PrivateProfile_Owner_ReturnsProfile()
    {
        int ownerId = await SeedUserAsync();
        await SetProfileVisibilityAsync(ownerId, ProfileVisibility.Private);
        SetActiveUser(ownerId);

        HttpClient client = Factory.CreateClient();
        // Client no longer even needs to pass includePrivate — the server derives it.
        HttpResponseMessage response = await client.GetAsync($"/api/user-profiles/{ownerId}");

        response.EnsureSuccessStatusCode();
        ProfileHeaderDto? header = await ReadNullableAsync<ProfileHeaderDto>(response);
        header.Should().NotBeNull("the owner must always be able to view their own private profile");
    }

    private async Task SeedBioAsync(int userId, string text)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.UserProfiles.Add(new UserProfile { UserId = userId, Text = text });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetBio_PrivateProfile_Anonymous_ReturnsNull()
    {
        int ownerId = await SeedUserAsync();
        await SeedBioAsync(ownerId, "<p>secret bio</p>");
        await SetProfileVisibilityAsync(ownerId, ProfileVisibility.Private);

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync($"/api/user-profiles/{ownerId}/bio");

        response.EnsureSuccessStatusCode();
        string? bio = await ReadNullableAsync<string>(response);
        bio.Should().BeNull("a Private profile's bio must not be readable via the raw /bio route");
    }

    [Fact]
    public async Task GetBio_PrivateProfile_Owner_ReturnsBio()
    {
        int ownerId = await SeedUserAsync();
        await SeedBioAsync(ownerId, "<p>my own bio</p>");
        await SetProfileVisibilityAsync(ownerId, ProfileVisibility.Private);
        SetActiveUser(ownerId);

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync($"/api/user-profiles/{ownerId}/bio");

        response.EnsureSuccessStatusCode();
        string? bio = await ReadNullableAsync<string>(response);
        bio.Should().Be("<p>my own bio</p>", "the owner always sees their own bio");
    }

    [Fact]
    public async Task GetProfile_HiddenLastSeen_OtherUserWithIncludePrivateTrue_StaysNull()
    {
        int ownerId = await SeedUserAsync();
        await SetActivityStatusAsync(ownerId, showActivityStatus: false, lastActiveUtc: DateTime.UtcNow);
        int otherId = await SeedUserAsync("other");
        SetActiveUser(otherId);

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync($"/api/user-profiles/{ownerId}?includePrivate=true");

        response.EnsureSuccessStatusCode();
        ProfileHeaderDto? header = await ReadNullableAsync<ProfileHeaderDto>(response);
        header.Should().NotBeNull();
        header!.LastSeenUtc.Should().BeNull(
            "a client-asserted includePrivate=true must not unlock another user's hidden last-seen timestamp");
    }
}
