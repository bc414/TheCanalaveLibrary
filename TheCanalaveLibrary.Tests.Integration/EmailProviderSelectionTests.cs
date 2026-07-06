using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// DI-resolution coverage for the default branch of the <c>Email:Provider</c> switch in
/// Program.cs (WU-Email) — same "does the host actually resolve the right implementation" shape
/// as <see cref="HostBootTests"/>.
///
/// <b>Only the default ("NoOp") branch is covered here, deliberately.</b> The switch reads
/// <c>builder.Configuration["Email:Provider"]</c> directly in Program.cs's top-level code, before
/// <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}.WithWebHostBuilder"/>'s
/// <c>ConfigureAppConfiguration</c> override can take effect — the identical "WebApplicationBuilder
/// quirk" <see cref="TestAppFactory"/>'s own class doc records for the connection string. This is
/// exactly why the parallel <c>ImageStorage:Provider</c> switch has no Integration-tier test for
/// its alternate ("S3") branch either: proving the alternate branch selects correctly means
/// booting the real process with the env var set before start, which is what the Aspire path's
/// <c>AppHost.cs</c> (<c>Email__Provider=Smtp</c> → Mailpit) does, verified live/manually rather
/// than through this harness.
/// </summary>
[Collection("Postgres")]
public class EmailProviderSelectionTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    [Fact]
    public void DefaultConfig_ResolvesTheNoOpSender()
    {
        using IServiceScope scope = Factory.Services.CreateScope();

        IEmailSender<User> sender = scope.ServiceProvider.GetRequiredService<IEmailSender<User>>();

        // IdentityNoOpEmailSender is internal (the repo deliberately carries no
        // InternalsVisibleTo — see SmtpEmailSender's class doc), so assert by type name rather
        // than a direct reference.
        sender.GetType().Name.Should().Be("IdentityNoOpEmailSender");
    }
}
