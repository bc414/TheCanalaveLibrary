using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// <see cref="ReportDialog"/>'s refusal surface (the WU-ErrorHandling2 follow-up, built by
/// WU-TrackerFixes 2026-10-13): a refused submission renders through <see cref="ErrorAlert"/>, which
/// offers Sign in only when the session expired, and the flag never outlives its message.
/// Tier: RazorComponents (bUnit).
/// </summary>
public class ReportDialogTests : BunitContext
{
    private readonly FakeReportSubmissionService _reports = new()
    {
        Reasons = [new ReportReasonDto(1, "Spam", null)],
    };

    public ReportDialogTests() => Services.AddScoped<IReportSubmissionService>(_ => _reports);

    private async Task<IRenderedComponent<ReportDialog>> OpenAndSubmitAsync()
    {
        IRenderedComponent<ReportDialog> cut = Render<ReportDialog>();
        await cut.InvokeAsync(() => cut.Instance.OpenAsync(ReportedEntityType.Story, 5));
        cut.Find("#report-reason").Change("1");
        await SubmitAsync(cut);
        return cut;
    }

    private static Task SubmitAsync(IRenderedComponent<ReportDialog> cut) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == "Submit Report")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

    private static int SignInLinks(IRenderedComponent<ReportDialog> cut) =>
        cut.FindAll("a").Count(a => a.TextContent.Trim() == "Sign in");

    [Fact]
    public async Task ExpiredSession_OffersSignIn_AndALaterRefusalDropsIt()
    {
        _reports.SubmitException = new SessionExpiredException();
        IRenderedComponent<ReportDialog> cut = await OpenAndSubmitAsync();

        cut.Find("[role='alert']").TextContent.Should().Contain("session has expired");
        SignInLinks(cut).Should().Be(1);

        _reports.SubmitException = new ModerationValidationException(
            ["You've already reported this — a moderator will review your open report."]);
        await SubmitAsync(cut);

        cut.Find("[role='alert']").TextContent.Should().Contain("already reported");
        SignInLinks(cut).Should().Be(0, "the flag travels with the message, so a validation refusal offers no Sign in");
    }

    [Fact]
    public async Task Reopening_ClearsTheRefusal_AndItsFlag()
    {
        _reports.SubmitException = new SessionExpiredException();
        IRenderedComponent<ReportDialog> cut = await OpenAndSubmitAsync();

        await cut.InvokeAsync(() => cut.Instance.OpenAsync(ReportedEntityType.Comment, 9));

        cut.FindAll("[role='alert']").Should().BeEmpty();
        SignInLinks(cut).Should().Be(0);
    }
}
