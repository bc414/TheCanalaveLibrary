using FluentAssertions;
using Microsoft.Extensions.Configuration;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Binding + default-value coverage for <see cref="EmailOptions"/> — the config shape Program.cs
/// binds from <c>Email</c> when the provider switch selects <c>Smtp</c> (see cross-cutting.md
/// "Identity & Auth"). No host/DB: a bare <see cref="ConfigurationBuilder"/> exercises the same
/// binder Program.cs uses via <c>services.Configure&lt;EmailOptions&gt;(...)</c>.
/// </summary>
public class EmailOptionsTests
{
    [Fact]
    public void SectionName_IsEmail()
    {
        EmailOptions.SectionName.Should().Be("Email");
    }

    [Fact]
    public void Binding_PopulatesTopLevelAndNestedSmtpProperties()
    {
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:FromAddress"] = "noreply@example.com",
                ["Email:FromName"] = "Test Sender",
                ["Email:Smtp:Host"] = "smtp.example.com",
                ["Email:Smtp:Port"] = "2525",
                ["Email:Smtp:User"] = "user1",
                ["Email:Smtp:Password"] = "secret",
                ["Email:Smtp:UseStartTls"] = "false",
            })
            .Build();

        var options = new EmailOptions();
        config.GetSection(EmailOptions.SectionName).Bind(options);

        options.FromAddress.Should().Be("noreply@example.com");
        options.FromName.Should().Be("Test Sender");
        options.Smtp.Host.Should().Be("smtp.example.com");
        options.Smtp.Port.Should().Be(2525);
        options.Smtp.User.Should().Be("user1");
        options.Smtp.Password.Should().Be("secret");
        options.Smtp.UseStartTls.Should().BeFalse();
    }

    [Fact]
    public void Defaults_AreSaneForAnUnconfiguredSection()
    {
        var options = new EmailOptions();

        options.FromAddress.Should().BeEmpty();
        options.FromName.Should().BeEmpty();
        options.Smtp.Host.Should().BeEmpty();
        // 587 (submission/STARTTLS) is the sane default port; UseStartTls defaults true to match —
        // Mailpit's dev wiring explicitly overrides both (see AppHost.cs).
        options.Smtp.Port.Should().Be(587);
        options.Smtp.User.Should().BeNull();
        options.Smtp.Password.Should().BeNull();
        options.Smtp.UseStartTls.Should().BeTrue();
    }
}
