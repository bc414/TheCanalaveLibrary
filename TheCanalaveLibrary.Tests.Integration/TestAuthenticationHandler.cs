using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Authenticates every request as whatever <see cref="FakeActiveUserContext"/> currently holds —
/// lets HTTP-boundary tests (<c>Factory.CreateClient()</c>) exercise real
/// <c>.RequireAuthorization()</c> gates via <see cref="IntegrationTestBase.SetActiveUser(int)"/>
/// instead of a real Identity cookie sign-in flow (which no test in this suite performs). Registered
/// in <see cref="TestAppFactory"/> as the default authentication scheme, replacing
/// <c>IdentityConstants.ApplicationScheme</c> for the test host only — <see cref="FakeActiveUserContext"/>
/// itself (the app-level "who is the current user" service) is unaffected and still the single source
/// of truth business logic reads from.
/// </summary>
public sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        FakeActiveUserContext fake = Context.RequestServices.GetRequiredService<FakeActiveUserContext>();
        if (!fake.IsAuthenticated || fake.UserId is not int userId)
            return Task.FromResult(AuthenticateResult.NoResult());

        List<Claim> claims = [new Claim(ClaimTypes.NameIdentifier, userId.ToString())];
        if (fake.IsModerator) claims.Add(new Claim(ClaimTypes.Role, "Moderator"));
        if (fake.IsAdmin) claims.Add(new Claim(ClaimTypes.Role, "Admin"));

        ClaimsIdentity identity = new(claims, SchemeName);
        AuthenticationTicket ticket = new(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
