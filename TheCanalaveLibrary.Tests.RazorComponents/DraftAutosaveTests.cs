using System.Text.Json;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Tests for <see cref="DraftAutosave"/> restore behaviour (cross-cutting.md §"Error Handling
/// Strategy" — editor draft safety) over bUnit's JSInterop stand-in for draft-autosave.js:
/// a leftover localStorage draft that differs from the loaded content offers a restore banner;
/// Restore hands the fields back to the editor; Discard clears the device copy; a backup
/// identical to the loaded content is silently cleared (nothing unsaved to offer). The 10s
/// autosave tick itself is deliberately not simulated (wall-clock flake); capture/save wiring
/// is covered by the restore path exercising the same serialization.
/// </summary>
public class DraftAutosaveTests : BunitContext
{
    private const string Key = "draft:test:1";

    public DraftAutosaveTests()
    {
        Services.AddScoped<DraftStore>();
        Services.AddSingleton<IToastService>(new ToastService());
        Services.AddLogging();
    }

    private static string EnvelopeJson(Dictionary<string, string?> fields)
        => JsonSerializer.Serialize(new DraftEnvelope(DateTime.UtcNow.AddMinutes(-5), fields),
            JsonSerializerOptions.Web);

    private IRenderedComponent<DraftAutosave> RenderAutosave(
        Dictionary<string, string?> currentContent,
        List<IReadOnlyDictionary<string, string?>>? restored = null)
        => Render<DraftAutosave>(p => p
            .Add(a => a.DraftKey, Key)
            .Add(a => a.Capture, () => Task.FromResult<Dictionary<string, string?>?>(currentContent))
            .Add(a => a.OnRestore, fields => restored?.Add(fields)));

    [Fact]
    public void NoStoredDraft_ShowsNoBanner()
    {
        JSInterop.Setup<string?>("canalaveDraft.load", Key).SetResult(null);

        IRenderedComponent<DraftAutosave> cut = RenderAutosave(new() { ["Title"] = "current" });

        cut.Markup.Should().NotContain("unsaved draft");
    }

    [Fact]
    public void StoredDraftDifferingFromLoadedContent_OffersRestore()
    {
        JSInterop.Setup<string?>("canalaveDraft.load", Key)
            .SetResult(EnvelopeJson(new() { ["Title"] = "typed but never submitted" }));

        IRenderedComponent<DraftAutosave> cut = RenderAutosave(new() { ["Title"] = "persisted version" });

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("unsaved draft"));
        cut.Markup.Should().Contain("Restore it");
    }

    [Fact]
    public void Restore_HandsTheDraftFieldsBackToTheEditor()
    {
        JSInterop.Setup<string?>("canalaveDraft.load", Key)
            .SetResult(EnvelopeJson(new() { ["Title"] = "recovered title" }));
        List<IReadOnlyDictionary<string, string?>> restored = [];

        IRenderedComponent<DraftAutosave> cut = RenderAutosave(new() { ["Title"] = "old" }, restored);
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Restore it"));

        cut.FindAll("button").First(b => b.TextContent.Contains("Restore")).Click();

        restored.Should().ContainSingle();
        restored[0]["Title"].Should().Be("recovered title");
        cut.Markup.Should().NotContain("unsaved draft"); // banner gone
    }

    [Fact]
    public void Discard_ClearsTheDeviceCopy()
    {
        JSInterop.Setup<string?>("canalaveDraft.load", Key)
            .SetResult(EnvelopeJson(new() { ["Title"] = "abandoned" }));
        JSInterop.SetupVoid("canalaveDraft.clear", Key).SetVoidResult();

        IRenderedComponent<DraftAutosave> cut = RenderAutosave(new() { ["Title"] = "current" });
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Discard"));

        cut.FindAll("button").First(b => b.TextContent.Contains("Discard")).Click();

        cut.WaitForAssertion(() => JSInterop.VerifyInvoke("canalaveDraft.clear"));
        cut.Markup.Should().NotContain("unsaved draft");
    }

    [Fact]
    public void StoredDraftIdenticalToLoadedContent_IsSilentlyCleared()
    {
        Dictionary<string, string?> same = new() { ["Title"] = "identical" };
        JSInterop.Setup<string?>("canalaveDraft.load", Key).SetResult(EnvelopeJson(same));
        JSInterop.SetupVoid("canalaveDraft.clear", Key).SetVoidResult();

        IRenderedComponent<DraftAutosave> cut = RenderAutosave(same);

        cut.WaitForAssertion(() => JSInterop.VerifyInvoke("canalaveDraft.clear"));
        cut.Markup.Should().NotContain("unsaved draft");
    }
}
