using Bunit;
using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// <see cref="PollEditorForm"/>'s refusal surface (the WU-ErrorHandling2 follow-up, built by
/// WU-TrackerFixes 2026-10-13): the page's errors render through <see cref="ErrorAlert"/>, with Sign
/// in only when the page says the session expired. Shared by <c>PollsPage</c> and
/// <c>BlogPostEditorPage</c>. No @inject — no DI setup beyond ErrorAlert's NavigationManager.
/// Tier: RazorComponents (bUnit).
/// </summary>
public class PollEditorFormTests : BunitContext
{
    private static PollEditDto NewModel() => new()
    {
        Options = [new PollOptionEditDto(null, "Yes"), new PollOptionEditDto(null, "No")],
    };

    private static int SignInLinks(IRenderedComponent<PollEditorForm> cut) =>
        cut.FindAll("a").Count(a => a.TextContent.Trim() == "Sign in");

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void Errors_RenderThroughErrorAlert_WithSignInOnlyForAnExpiredSession(bool sessionExpired, int signInLinks)
    {
        IRenderedComponent<PollEditorForm> cut = Render<PollEditorForm>(p => p
            .Add(c => c.Model, NewModel())
            .Add(c => c.Errors, ["The poll could not be saved."])
            .Add(c => c.ErrorsIsSessionExpired, sessionExpired));

        cut.Find("[role='alert']").TextContent.Should().Contain("The poll could not be saved.");
        SignInLinks(cut).Should().Be(signInLinks);
    }

    [Fact]
    public void NoErrors_RenderNoAlert()
    {
        IRenderedComponent<PollEditorForm> cut = Render<PollEditorForm>(p => p
            .Add(c => c.Model, NewModel())
            .Add(c => c.ErrorsIsSessionExpired, true));

        cut.FindAll("[role='alert']").Should().BeEmpty("ErrorAlert self-hides with no message");
        SignInLinks(cut).Should().Be(0);
    }
}
