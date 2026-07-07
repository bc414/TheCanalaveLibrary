using Bunit;
using FluentAssertions;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="InlineAlert"/> — the standard inline feedback atom
/// (cross-cutting.md §"Error Handling Strategy"). Self-hides when empty; single message renders
/// as a paragraph, multiple as a list; variant maps to the palette classes.
/// </summary>
public class InlineAlertTests : BunitContext
{
    [Fact]
    public void NoMessages_RendersNothing()
    {
        IRenderedComponent<InlineAlert> cut = Render<InlineAlert>();

        cut.Markup.Trim().Should().BeEmpty();
    }

    [Fact]
    public void BlankMessages_RenderNothing()
    {
        IRenderedComponent<InlineAlert> cut = Render<InlineAlert>(p => p
            .Add(a => a.Message, "  ")
            .Add(a => a.Messages, new List<string> { "" }));

        cut.Markup.Trim().Should().BeEmpty();
    }

    [Fact]
    public void SingleMessage_RendersAsParagraph_WithAlertRole()
    {
        IRenderedComponent<InlineAlert> cut = Render<InlineAlert>(p => p.Add(a => a.Message, "Server says no."));

        cut.Find("[role='alert'] p").TextContent.Should().Be("Server says no.");
    }

    [Fact]
    public void MultipleMessages_RenderAsList()
    {
        IRenderedComponent<InlineAlert> cut = Render<InlineAlert>(p =>
            p.Add(a => a.Messages, new List<string> { "First problem.", "Second problem." }));

        cut.FindAll("li").Should().HaveCount(2);
    }

    [Fact]
    public void DangerIsDefault_SuccessSwapsPalette()
    {
        IRenderedComponent<InlineAlert> danger = Render<InlineAlert>(p => p.Add(a => a.Message, "x"));
        IRenderedComponent<InlineAlert> success = Render<InlineAlert>(p => p
            .Add(a => a.Message, "x")
            .Add(a => a.Variant, InlineAlertVariant.Success));

        danger.Find("[role='alert']").GetAttribute("class").Should().Contain("--color-danger");
        success.Find("[role='alert']").GetAttribute("class").Should().Contain("--color-success");
    }
}
