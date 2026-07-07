using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Containment tests for <see cref="CanalaveErrorBoundary"/> (cross-cutting.md §"Error Handling
/// Strategy"): a child fault renders the fallback instead of propagating (the circuit-survival
/// guarantee at component scope), the fault is logged at Error with the island label, Try again
/// re-renders the child, and navigation auto-recovers a faulted island.
/// </summary>
public class CanalaveErrorBoundaryTests : BunitContext
{
    private readonly CapturingLoggerProvider _logs = new();

    public CanalaveErrorBoundaryTests()
    {
        Services.AddLogging(b => b.AddProvider(_logs));
    }

    // A child whose lifecycle throws while ShouldThrow is true.
    private sealed class ThrowingChild : ComponentBase
    {
        [Parameter] public bool ShouldThrow { get; set; }

        protected override void OnParametersSet()
        {
            if (ShouldThrow) throw new InvalidOperationException("boom");
        }

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
            => builder.AddContent(0, "child-content");
    }

    private IRenderedComponent<CanalaveErrorBoundary> RenderBoundary(bool childThrows, bool compact = false)
        => Render<CanalaveErrorBoundary>(p => p
            .Add(b => b.Label, "test-island")
            .Add(b => b.Compact, compact)
            .AddChildContent<ThrowingChild>(c => c.Add(t => t.ShouldThrow, childThrows)));

    [Fact]
    public void ChildFault_RendersFallback_InsteadOfPropagating()
    {
        IRenderedComponent<CanalaveErrorBoundary> cut = RenderBoundary(childThrows: true);

        cut.Markup.Should().Contain("Try again");
        cut.Markup.Should().NotContain("child-content");
        cut.Find("[role='alert']").Should().NotBeNull();
    }

    [Fact]
    public void ChildFault_IsLoggedAtError_WithIslandLabel()
    {
        RenderBoundary(childThrows: true);

        _logs.Entries.Should().ContainSingle(e =>
            e.Level == LogLevel.Error && e.Message.Contains("test-island"));
    }

    [Fact]
    public void HealthyChild_RendersNormally_NoFallback()
    {
        IRenderedComponent<CanalaveErrorBoundary> cut = RenderBoundary(childThrows: false);

        cut.Markup.Should().Contain("child-content");
        cut.Markup.Should().NotContain("Try again");
        _logs.Entries.Should().BeEmpty();
    }

    [Fact]
    public void CompactFault_RendersOneLinerFallback()
    {
        IRenderedComponent<CanalaveErrorBoundary> cut = RenderBoundary(childThrows: true, compact: true);

        cut.Markup.Should().Contain("This section hit an error.");
    }

    [Fact]
    public void TryAgain_RecoversAndReRendersChild_WhenFaultCleared()
    {
        IRenderedComponent<CanalaveErrorBoundary> cut = RenderBoundary(childThrows: true);
        cut.Markup.Should().Contain("Try again");

        // Clear the fault condition, then user-gesture recover.
        cut.Render(p => p
            .AddChildContent<ThrowingChild>(c => c.Add(t => t.ShouldThrow, false)));
        cut.Find("button").Click();

        cut.Markup.Should().Contain("child-content");
        cut.Markup.Should().NotContain("Try again");
    }

    [Fact]
    public void Navigation_AutoRecoversAFaultedIsland()
    {
        IRenderedComponent<CanalaveErrorBoundary> cut = RenderBoundary(childThrows: true);
        cut.Markup.Should().Contain("Try again");

        // Fault condition gone (new page's content), then a navigation event lands.
        cut.Render(p => p
            .AddChildContent<ThrowingChild>(c => c.Add(t => t.ShouldThrow, false)));
        Services.GetRequiredService<NavigationManager>().NavigateTo("/somewhere-else");

        cut.Markup.Should().Contain("child-content");
        cut.Markup.Should().NotContain("Try again");
    }
}

/// <summary>Minimal capturing logger for render-tier tests (FakeLogger lives in the Unit tier).</summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    public sealed record Entry(LogLevel Level, string Message, Exception? Exception);

    public List<Entry> Entries { get; } = [];

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(Entries);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(List<Entry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => entries.Add(new Entry(logLevel, formatter(state, exception), exception));
    }
}
