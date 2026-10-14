using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Drives an already-open <c>ReportDialog</c> inside any host: picks a reason and submits. A host test
/// then asserts the <see cref="FakeReportSubmissionService.Submitted"/> request, which is what proves the
/// host wired the right <c>ReportedEntityType</c> and id, not merely that a dialog opened
/// (WU-TrackerFixes review fixes).
/// </summary>
public static class ReportDialogDriver
{
    public static async Task SubmitAsync<TComponent>(IRenderedComponent<TComponent> cut, short reasonId = 1)
        where TComponent : IComponent
    {
        cut.Find("#report-reason").Change(reasonId.ToString());
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Submit Report")
            .ClickAsync(new MouseEventArgs());
    }
}
